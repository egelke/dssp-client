﻿/*
 *  This file is part of DSS-P client.
 *  Copyright (C) 2014-2017 Egelke BVBA
 *  Copyright (C) 2014-2016 e-Contract.be BVBA
 *
 *  DSS-P client is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU Lesser General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  DSS-P client is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU Lesser General Public License for more details.
 *
 *  You should have received a copy of the GNU Lesser General Public License
 *  along with DSS-P client.  If not, see <http://www.gnu.org/licenses/>.
 */

using EContract.Dssp.Client.Proxy;
using EContract.Dssp.Client.WcfBinding;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.Xml.Serialization;
#if NET45_OR_GREATER
using System.Threading.Tasks;
#endif

namespace EContract.Dssp.Client
{
    /// <summary>
    /// The DSS-P client for e-contract.
    /// </summary>
    /// <remarks>
    /// This is a wrapper class for the e-contract service.  It does not contain direct support of the BROWSER/POST protocol, but provides the necessary input
    /// and processes its output.
    /// </remarks>
    public partial class DsspClient
    {

        private readonly Random rand = new Random();
        private XmlSerializer requestSerializer = new XmlSerializer(typeof(PendingRequest), "urn:oasis:names:tc:dss:1.0:profiles:asynchronousprocessing:1.0");
        private XmlSerializer responseSerializer = new XmlSerializer(typeof(SignResponse), "urn:oasis:names:tc:dss:1.0:core:schema");

        /// <summary>
        /// The e-contract signature type.
        /// </summary>
        /// <value>
        /// Can be empty (the default) at which e-contract will select the most appropriate signature type.
        /// </value>
        public string SignatureType { get; set; }

        /// <summary>
        /// The address of e-contract DSS-P service.
        /// </summary>
        public EndpointAddress Address { get; set; }

        /// <summary>
        /// The user name of your (the DSS-P client) application.  This is optional.
        /// </summary>
        [Obsolete("Use Application instead")]
        public string ApplicationName
        {
            get
            {
                return Application.UT.Name;
            }
            set
            {
                Application.UT.Name = value;
            }
        }


        /// <summary>
        /// The password of your (the DSS-P client) application.  This is optional.
        /// </summary>
        [Obsolete("Use Application instead")]
        public string ApplicationPassword
        {
            get
            {
                return Application.UT.Password;
            }
            set
            {
                Application.UT.Password = value;
            }
        }

        /// <summary>
        /// Credentials of your (the DSS-P client) application.
        /// </summary>
        public AppCredentials Application { get; }

        /// <summary>
        /// The certificate to use in case of two step local signature.
        /// </summary>
        /// <remarks>
        /// Only used when <c>SignerChain</c> is not provided, the chain
        /// is then constructed from the windows certificate store.
        /// </remarks>
        /// <seealso cref="SignerChain"/>
        [Obsolete("Signer is deprecated, please use SignerChain instead")]
        public X509Certificate2 Signer
        {
            get
            {
                if (SignerChain?.Length > 1)
                    throw new NotSupportedException("This property isn't supported with a full chain");

                return SignerChain?[0];
            }
            set
            {
                SignerChain = new X509Certificate2[] { value };
            }
        }

        /// <summary>
        /// The entire chain of certificiates to be used for 2 stap local signature.
        /// </summary>
        /// <value>
        /// The full chain of certificates (e.g. cert 0=end cert, cert1=intermediate CA, cert2 = root CA) or just the end cert
        /// </value>
        /// <remarks>
        /// When more then 1 cert is provided a full chain is assumed, when only 1 cert is provided the chain will be constructed from the windows certificate store.
        /// </remarks>
        public X509Certificate2[] SignerChain { get; set; }


        /// <summary>
        /// Basic constructor.
        /// </summary>
        /// <param name="address">The address of the e-contract DSS-P service</param>
        public DsspClient(string address)
            : this(new EndpointAddress(address))
        {

        }

        /// <summary>
        /// Basic constructor.
        /// </summary>
        /// <remarks>
        /// Client with default signature type for the specified address.
        /// </remarks>
        /// <param name="address">The address of the e-contract DSS-P service</param>
        public DsspClient(EndpointAddress address)
            : this(address, null)
        {

        }

        /// <summary>
        /// Full Constructor
        /// </summary>
        /// <param name="address">The address of the e-contract DSS-P service</param>
        /// <param name="signatureType">The signature type that is required, see e-contract documentation</param>
        public DsspClient(EndpointAddress address, string signatureType)
        {
            this.Address = address;
            this.SignatureType = SignatureType;
            this.Application = new AppCredentials();
        }

        /// <summary>
        /// Uploads a document to e-Contract for online signature.
        /// </summary>
        /// <remarks>
        /// Uploads a document to e-Contract and returns the session for future references.
        /// </remarks>
        /// <param name="document">The document to be signed</param>
        /// <returns>The session, required for the BROWSER/POST protocol and the download of the signed message</returns>
        public DsspSession UploadDocument(Document document)
        {
            if (document == null) throw new ArgumentNullException("document");

            var client = CreateDSSPClient();
            var request = CreateAsyncSignRequest(document, out var clientNonce);
            SignResponse response = client.sign(request);
            return ProcessAsyncSignResponse(response, clientNonce);
        }

#if NET45_OR_GREATER
        /// <summary>
        /// Uploads a document to e-Contract, asynchronously.
        /// </summary>
        /// <see cref="UploadDocument"/>
        public async Task<DsspSession> UploadDocumentAsync(Document document)
        {
            if (document == null) throw new ArgumentNullException("document");

            var client = CreateDSSPClient();
            var request = CreateAsyncSignRequest(document, out var clientNonce);
            signResponse1 responseWrapper = await client.signAsync(request);
            return ProcessAsyncSignResponse(responseWrapper.SignResponse, clientNonce);
        }
#endif

        /// <summary>
        /// Uploads the document to e-Contract for offline signature.
        /// </summary>
        /// <remarks>
        /// Uploads a document to e-Contract and returns the session for easy signing.
        /// </remarks>
        /// <param name="document">The document to be signed</param>
        /// <returns>The session, required to calculate the signature</returns>
        public Dssp2StepSession UploadDocumentFor2Step(Document document)
        {
            return UploadDocumentFor2Step(document, null);
        }

#if NET45_OR_GREATER
        /// <summary>
        /// Uploads a document to e-Contract, asynchronously.
        /// </summary>
        /// <see cref="UploadDocumentFor2Step(Document)"/>
        public async Task<Dssp2StepSession> UploadDocumentFor2StepAsync(Document document)
        {
            return await UploadDocumentFor2StepAsync(document, null);
        }
#endif

        /// <summary>
        /// Uploads the document to e-Contract for offline signature.
        /// </summary>
        /// <remarks>
        /// Uploads a document to e-Contract and returns the session for easy signing.
        /// </remarks>
        /// <param name="document">The document to be signed</param>
        /// <param name="properties">additional signing properties like location, role and visual signature</param>
        /// <returns>The session, required to calculate the signature</returns>
        public Dssp2StepSession UploadDocumentFor2Step(Document document, SignatureRequestProperties properties)
        {
            if (document == null) throw new ArgumentNullException("document");
            if ((SignerChain?.Length ?? 0) == 0 ||
                SignerChain[0] == null ||
                !SignerChain[0].HasPrivateKey)
                throw new InvalidOperationException("SignerChain must be set and the end (first) certificate must have a private key");

            var client = CreateDSSPClient();
            var request = Create2StepSignRequest(document, properties);
            SignResponse response = client.sign(request);
            return Process2StepSignResponse(response);
        }

#if NET45_OR_GREATER
        /// <summary>
        /// Uploads a document to e-Contract, asynchronously.
        /// </summary>
        /// <see cref="UploadDocumentFor2Step(Document, SignatureRequestProperties)"/>
        public async Task<Dssp2StepSession> UploadDocumentFor2StepAsync(Document document, SignatureRequestProperties properties)
        {
            if (document == null) throw new ArgumentNullException("document");
            if (!(SignerChain?[0]?.HasPrivateKey ?? false && SignerChain?[0]?.PrivateKey is RSACryptoServiceProvider)) throw new InvalidOperationException("Singner must be set and have a private key");

            var client = CreateDSSPClient();
            var request = Create2StepSignRequest(document, properties);
            signResponse1 response = await client.signAsync(request);
            return Process2StepSignResponse(response.SignResponse);
        }
#endif

        /// <summary>
        /// Downloads the document that was uploaded before and signed via the BROWSER/POST protocol.
        /// </summary>
        /// <remarks>
        /// The session is closed when the downloads finishes, it can't be reused afterward and should be removed from the storage.
        /// </remarks>
        /// <param name="session">The session linked to the uploaded document</param>
        /// <returns>The document with signature, including id and mimeType</returns>
        /// <exception cref="ArgumentException">When the signResponse isn't valid, including its signature</exception>
        /// <exception cref="InvalidOperationException">When the e-contract service returns an error</exception>
        public Document DownloadDocument(DsspSession session)
        {
            if (session == null) throw new ArgumentNullException("session");

            var client = CreateDSSPClient(session);
            var downloadRequest = CreateDownloadRequest(session);
            SignResponse downloadResponse = client.pendingRequest(downloadRequest);
            return ProcessResponseWithSignedDoc(downloadResponse);
        }

#if NET45_OR_GREATER
        /// <summary>
        /// Downloads the document that was uploaded before and signed via the BROWSER/POST protocol, asynchronously.
        /// </summary>
        /// <see cref="DownloadDocument(DsspSession)"/>
        public async Task<Document> DownloadDocumentAsync(DsspSession session)
        {
            if (session == null) throw new ArgumentNullException("session");

            var client = CreateDSSPClient(session);
            var downloadRequest = CreateDownloadRequest(session);
            pendingRequestResponse downloadResponseWrapper = await client.pendingRequestAsync(downloadRequest);
            return ProcessResponseWithSignedDoc(downloadResponseWrapper.SignResponse);
        }
#endif

        /// <summary>
        /// Downloads the document that was uploaded before and signed offline.
        /// </summary>
        /// <remarks>
        /// The session is closed when the downloads finishes, it can't be reused afterward and should be removed from the storage.
        /// </remarks>
        /// <param name="session">The session linked to the uploaded document</param>
        /// <returns>The document with signature, including id and mimeType</returns>
        /// <exception cref="ArgumentException">When the signResponse isn't valid, including its signature</exception>
        /// <exception cref="InvalidOperationException">When the e-contract service returns an error</exception>
        public Document DownloadDocument(Dssp2StepSession session)
        {
            if (session == null) throw new ArgumentNullException("session");

            var client = CreateDSSPClient();
            var downloadRequest = CreateDownloadRequest(session);
            SignResponse downloadResponse = client.sign(downloadRequest);
            return ProcessResponseWithSignedDoc(downloadResponse);
        }

#if NET45_OR_GREATER
        /// <summary>
        /// Downloads the document that was uploaded before and signed offline.
        /// </summary>
        /// <see cref="DownloadDocument(Dssp2StepSession)"/>
        public async Task<Document> DownloadDocumentAsync(Dssp2StepSession session)
        {
            if (session == null) throw new ArgumentNullException("session");

            var client = CreateDSSPClient();
            var downloadRequest = CreateDownloadRequest(session);
            signResponse1 downloadResponse = await client.signAsync(downloadRequest);
            return ProcessResponseWithSignedDoc(downloadResponse.SignResponse);
        }
#endif

        /// <summary>
        /// Validates the provided document via the e-contract service.
        /// </summary>
        /// <param name="document">The document that contains a signature</param>
        /// <returns>The security information of the document, containing information like the signer</returns>
        /// <exception cref="ArgumentNullException">When there is no document provided</exception>
        /// <exception cref="IncorrectSignatureException">When the provided document has an invalid signature</exception>
        /// <exception cref="RequestError">When the request was invalid, e.g. unsupported mime type</exception>
        /// <exception cref="InvalidOperationException">All other errors indicated by the service</exception>
        public SecurityInfo Verify(Document document)
        {
            if (document == null) throw new ArgumentNullException("document");

            var client = CreateDSSPClient();
            var request = CreateVerifyRequest(document);
            ResponseBaseType response = client.verify(request);
            return ProcessVerifyResponse(response);
        }

#if NET45_OR_GREATER
        /// <summary>
        /// Validates the provided document via the e-contract service, asynchronously.
        /// </summary>
        /// <see cref="Verify"/>
        public async Task<SecurityInfo> VerifyAsync(Document document)
        {
            if (document == null) throw new ArgumentNullException("document");

            var client = CreateDSSPClient();
            var request = CreateVerifyRequest(document);
            verifyResponse responseWrapper = await client.verifyAsync(request);
            return ProcessVerifyResponse(responseWrapper.VerifyResponse1);
        }
#endif

        /// <summary>
        /// Add an eSeal to the document via the e-contract service.
        /// </summary>
        /// <remarks>
        /// The application should authenticate, based on this authentication, the Digital Signature Service will 
        /// select a key to be used to seal the given document.
        /// </remarks>
        /// <param name="document">The document to seal</param>
        /// <returns>The sealed document</returns>
        public Document Seal(Document document)
        {
            return Seal(document, null);
        }



        /// <summary>
        /// Add an eSeal to the document via the e-contract service.
        /// </summary>
        /// <remarks>
        /// The application should authenticate, based on this authentication, the Digital Signature Service will 
        /// select a key to be used to seal the given document.
        /// </remarks>
        /// <param name="document">The document to seal</param>
        /// <param name="properties">Signature properties</param>
        /// <returns>The sealed document</returns>
        public Document Seal(Document document, SignatureRequestProperties properties)
        {
            if (document == null) throw new ArgumentNullException("document");

            var client = CreateDSSPClient();
            var request = CreateSealRequest(document, properties);
            SignResponse response = client.sign(request);
            return ProcessResponseWithSignedDoc(response);
        }

#if NET45_OR_GREATER
        /// <summary>
        /// Add an eSeal to the document via the e-contract service.
        /// </summary>
        /// <see cref="Seal(Document, SignatureRequestProperties)"/>
        public async Task<Document> SealAsync(Document document, SignatureRequestProperties properties)
        {
            if (document == null) throw new ArgumentNullException("document");

            var client = CreateDSSPClient();
            var request = CreateSealRequest(document, properties);
            signResponse1 responseWrapper = await client.signAsync(request);
            return ProcessResponseWithSignedDoc(responseWrapper.SignResponse);
        }
#endif

        private DigitalSignatureServicePortTypeClient CreateDSSPClient()
        {
            DigitalSignatureServicePortTypeClient client;
            if (string.IsNullOrEmpty(this.Application.UT.Password)
                && this.Application.X509.Certificate == null
                && this.Application.X509.FindValue == null)
            {
                client = new DigitalSignatureServicePortTypeClient(new PlainDsspBinding(), Address);
            }
            else if (this.Application.X509.Certificate != null)
            {
                client = new DigitalSignatureServicePortTypeClient(new X509DsspBinding(), Address);
                client.ClientCredentials.ClientCertificate.Certificate = this.Application.X509.Certificate;

            }
            else if (this.Application.X509.FindValue != null)
            {
                client = new DigitalSignatureServicePortTypeClient(new X509DsspBinding(), Address);
                client.ClientCredentials.ClientCertificate.SetCertificate(this.Application.X509.StoreLocation,
                     this.Application.X509.StoreName, this.Application.X509.FindType, this.Application.X509.FindValue);
            }
            else
            {
                client = new DigitalSignatureServicePortTypeClient(new UTDsspBinding(), Address);
                client.ClientCredentials.UserName.UserName = this.Application.UT.Name;
                client.ClientCredentials.UserName.Password = this.Application.UT.Password;
            }
            return client;
        }

        private DigitalSignatureServicePortTypeClient CreateDSSPClient(DsspSession session)
        {
            var client = new DigitalSignatureServicePortTypeClient(new ScDsspBinding(), Address);
            client.ChannelFactory.Endpoint.Behaviors.Remove<ClientCredentials>();
            client.ChannelFactory.Endpoint.Behaviors.Add(new ScDsspClientCredentials(session.KeyId, session.KeyValue));
            return client;
        }

        private SignRequest CreateAsyncSignRequest(Document document, out byte[] clientNonce)
        {
            var documentId = "doc-" + Guid.NewGuid().ToString();

            clientNonce = new byte[32];
            rand.NextBytes(clientNonce);

            return new SignRequest()
            {
                Profile = "urn:be:e-contract:dssp:1.0",
                OptionalInputs = new OptionalInputs()
                {
                    AdditionalProfile = "urn:oasis:names:tc:dss:1.0:profiles:asynchronousprocessing",
                    RequestSecurityToken = new RequestSecurityTokenType()
                    {
                        TokenType = "http://docs.oasis-open.org/ws-sx/ws-secureconversation/200512/sct",
                        RequestType = "http://docs.oasis-open.org/ws-sx/ws-trust/200512/Issue",
                        Entropy = new EntropyType()
                        {
                            BinarySecret = new BinarySecretType()
                            {
                                Type = "http://docs.oasis-open.org/ws-sx/ws-trust/200512/Nonce",
                                Value = clientNonce
                            }
                        }
                    },
                    SignatureType = SignatureType,
                    SignaturePlacement = CreateEnvelopedSignature(documentId)
                },
                InputDocuments = new InputDocuments()
                {
                    Document = new DocumentType[]
                    {
                        CreateDocumentType(documentId, document)
                    }
                }
            };
        }

        private SignRequest CreateSealRequest(Document document, SignatureRequestProperties properties)
        {
            var documentId = "doc-" + Guid.NewGuid().ToString();

            return new SignRequest()
            {
                Profile = "urn:be:e-contract:dssp:eseal:1.0",
                OptionalInputs = new OptionalInputs()
                {
                    SignatureType = SignatureType,
                    SignaturePlacement = CreateEnvelopedSignature(documentId),
                    VisibleSignatureConfiguration = properties?.Configuration
                },
                InputDocuments = new InputDocuments()
                {
                    Document = new DocumentType[] {
                        CreateDocumentType(documentId, document)
                    }
                }
            };
        }

        private SignRequest Create2StepSignRequest(Document document, SignatureRequestProperties properties)
        {
            var documentId = "doc-" + Guid.NewGuid().ToString();

            byte[][] x509Chain;
            if (SignerChain.Length == 1 && SignerChain[0].Issuer != SignerChain[0].Subject)
            {
                var chain = X509Chain.Create();
                chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllFlags;
                chain.Build(SignerChain[0]);

                x509Chain = chain.ChainElements
                    .Cast<X509ChainElement>()
                    .AsQueryable()
                    .Select(x => x.Certificate.RawData)
                    .ToArray();
            } else {
                x509Chain = SignerChain.AsQueryable()
                    .Select(x => x.RawData)
                    .ToArray();
            }

            return new SignRequest()
            {
                Profile = "http://docs.oasis-open.org/dss-x/ns/localsig",
                OptionalInputs = new OptionalInputs()
                {
                    SignatureType = SignatureType,
                    ServicePolicy = "http://docs.oasis-open.org/dss-x/ns/localsig/two-step-approach",
                    SignaturePlacement = CreateEnvelopedSignature(documentId),
                    RequestDocumentHash = new RequestDocumentHash()
                    {
                        MaintainRequestState = true,
                        MaintainRequestStateSpecified = true
                    },
                    KeySelector = new KeySelector()
                    {
                        KeyInfo = new KeyInfoType()
                        {
                            X509Data = x509Chain
                        }
                    },
                    VisibleSignatureConfiguration = properties?.Configuration
                },
                InputDocuments = new InputDocuments()
                {
                    Document = new DocumentType[]
                    {
                        CreateDocumentType(documentId, document)
                    }
                }
            };
        }

        private PendingRequest CreateDownloadRequest(DsspSession session)
        {
            return new PendingRequest()
            {
                OptionalInputs = new OptionalInputs()
                {
                    AdditionalProfile = "urn:oasis:names:tc:dss:1.0:profiles:asynchronousprocessing",
                    ResponseID = session.ServerId,
                    RequestSecurityToken = new RequestSecurityTokenType()
                    {
                        RequestType = "http://docs.oasis-open.org/ws-sx/ws-trust/200512/Cancel",
                        CancelTarget = new CancelTargetType()
                        {
                            SecurityTokenReference = new SecurityTokenReferenceType()
                            {
                                Reference = new ReferenceType()
                                {
                                    ValueType = "http://docs.oasis-open.org/ws-sx/ws-secureconversation/200512/sct",
                                    URI = session.KeyId
                                }
                            }
                        }
                    }
                }
            };
        }

        private SignRequest CreateDownloadRequest(Dssp2StepSession session)
        {
            return new SignRequest()
            {
                Profile = "http://docs.oasis-open.org/dss-x/ns/localsig",
                OptionalInputs = new OptionalInputs()
                {
                    SignatureType = SignatureType,
                    ServicePolicy = "http://docs.oasis-open.org/dss-x/ns/localsig/two-step-approach",
                    CorrelationID = session.CorrelationId,
                    SignatureObject = new SignatureObject()
                    {
                        Base64Signature = new Base64Signature()
                        {
                            Value = session.SignValue
                        }
                    }
                }
            };
        }

        private void VerifyResponse(ResponseBaseType response, String ExpectedResultMajor, String ExpectedResultMinor)
        {
            if (response?.Result?.ResultMajor == ExpectedResultMajor)
            {
                if (ExpectedResultMinor != null && response?.Result?.ResultMinor != ExpectedResultMinor)
                {
                    throw new InvalidOperationException(response?.Result?.ResultMinor
                        + ": " + response?.Result?.ResultMessage?.Value);
                }
            }
            else
            {
                throw new InvalidOperationException(response.Result.ResultMajor + " " + response.Result.ResultMinor
                    + ": " + response?.Result?.ResultMessage?.Value);
            }
        }

        private DsspSession ProcessAsyncSignResponse(SignResponse response, byte[] clientNonce)
        {
            //Check response
            VerifyResponse(response, "urn:oasis:names:tc:dss:1.0:profiles:asynchronousprocessing:resultmajor:Pending", null);

            //Capture session info & store it
            var securityTokenResponse = response.OptionalOutputs.RequestSecurityTokenResponseCollection.RequestSecurityTokenResponse[0];
            return new DsspSession()
            {
                ServerId = response.OptionalOutputs.ResponseID,
                KeyId = securityTokenResponse.RequestedSecurityToken.SecurityContextToken.Identifier,
                KeyValue = new Psha1DerivedKeyGenerator(clientNonce).GenerateDerivedKey(securityTokenResponse.Entropy.BinarySecret.Value, (int)securityTokenResponse.KeySize),
                KeyReference = securityTokenResponse.RequestedUnattachedReference.SecurityTokenReference,
                ExpiresOn = securityTokenResponse.Lifetime.Expires.Value
            };
        }

        private Dssp2StepSession Process2StepSignResponse(SignResponse signResponse)
        {
            //check the download response
            VerifyResponse(signResponse, "urn:oasis:names:tc:dss:1.0:resultmajor:Success", "urn:oasis:names:tc:dss:1.0:resultminor:documentHash");

            //Capture session info & store it
            return new Dssp2StepSession()
            {
                Signer = this.SignerChain?[0],
                CorrelationId = signResponse.OptionalOutputs.CorrelationID,
                DigestAlgo = signResponse.OptionalOutputs.DocumentHash?.DigestMethod?.Algorithm,
                DigestValue = signResponse.OptionalOutputs.DocumentHash?.DigestValue
            };
        }

        private Document ProcessResponseWithSignedDoc(SignResponse downloadResponse)
        {
            //check the download response
            VerifyResponse(downloadResponse, "urn:oasis:names:tc:dss:1.0:resultmajor:Success", null);

            //Return the downloaded document (we assume there is only a single document)
            return new Document(downloadResponse.OptionalOutputs.DocumentWithSignature.Document);
        }

        private VerifyRequest CreateVerifyRequest(Document document)
        {
            return new VerifyRequest()
            {
                Profile = "urn:be:e-contract:dssp:1.0",
                OptionalInputs = new OptionalInputs()
                {
                    ReturnVerificationReport = new ReturnVerificationReport()
                    {
                        IncludeVerifier = true,
                        IncludeCertificateValues = true
                    }
                },
                InputDocuments = new InputDocuments()
                {
                    Document = new DocumentType[] {
                        CreateDocumentType("doc-" + Guid.NewGuid().ToString(), document)
                    }
                }
            };
        }

        private SecurityInfo ProcessVerifyResponse(ResponseBaseType response)
        {
            //Check response
            VerifyResponse(response, "urn:oasis:names:tc:dss:1.0:resultmajor:Success", null);

            //Is there security info?
            if (response.OptionalOutputs == null
                || response.OptionalOutputs.VerificationReport == null
                || response.OptionalOutputs.VerificationReport.IndividualReport == null)
            {
                return null;
            }

            SecurityInfo result = new SecurityInfo()
            {
                TimeStampValidity = response.OptionalOutputs.TimeStampRenewal?.Before ?? DateTime.MaxValue,
                Signatures = new List<SignatureInfo>()
            };
            foreach (var report in response.OptionalOutputs.VerificationReport.IndividualReport)
            {
                //double check
                if (report.Result.ResultMajor != "urn:oasis:names:tc:dss:1.0:resultmajor:Success") throw new InvalidOperationException(report.Result.ResultMajor);

                result.Signatures.Add(new SignatureInfo()
                {
                    SigningTime = DateTime.Parse(report.SignedObjectIdentifier.SignedProperties.SignedSignatureProperties.SigningTime, CultureInfo.InvariantCulture),
                    Signer = new X509Certificate2(report.Details.DetailedSignatureReport.CertificatePathValidity.PathValidityDetail.CertificateValidity[0].CertificateValue),
                    SignerSubject = report.Details.DetailedSignatureReport.CertificatePathValidity.PathValidityDetail.CertificateValidity[0].Subject,
                    SignatureProductionPlace = report.SignedObjectIdentifier.SignedProperties.SignedSignatureProperties.Location,
                    SignerRole = report.SignedObjectIdentifier.SignedProperties.SignedSignatureProperties.SignerRole == null ? null
                        : String.Join(", ", report.SignedObjectIdentifier.SignedProperties.SignedSignatureProperties.SignerRole.ClaimedRoles)
                });
            }
            return result;
        }

        private SignaturePlacement CreateEnvelopedSignature(String documentId)
        {
            return new SignaturePlacement()
            {
                CreateEnvelopedSignature = true,
                CreateEnvelopedSignatureSpecified = true,
                WhichDocument = documentId
            };

        }

        private DocumentType CreateDocumentType(String documentId, Document document)
        {
            var memStream = document.Content as MemoryStream;
            if (memStream == null)
            {
                memStream = new MemoryStream();
                document.Content.CopyTo(memStream);
            }
            var value = memStream.ToArray();

            return new DocumentType()
            {
                ID = documentId,
                Base64Data = new Base64Data()
                {
                    MimeType = document.MimeType,
                    Value = value
                }
            };
        }
    }
}
