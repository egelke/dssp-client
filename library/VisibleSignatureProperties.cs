﻿namespace EContract.Dssp.Client
{
    /// <summary>
    /// Base class to define a visible signature.
    /// </summary>
    public abstract class VisibleSignatureProperties
    {
        /// <summary>
        /// The page for visible signatures. Page starts at 1.
        /// </summary>
        public int Page { get; set; }

        /// <summary>
        /// The x location for visible signatures.
        /// </summary>
        public int X { get; set; }

        /// <summary>
        /// The y location for visible signatures.
        /// </summary>
        public int Y { get; set; }
    }

    /// <summary>
    /// Visible signature that exist of the a photo (the eID photo).
    /// </summary>
    public class ImageVisibleSignature : VisibleSignatureProperties
    {
        /// <summary>
        /// The URI of the photo, defaults to the eID photo.
        /// </summary>
        /// <value>
        /// <c>urn:be:e-contract:dssp:1.0:vs:si:eid-photo</c> for eID photo (defaults).
        /// </value>
        public string ValueUri { get; set; }

        /// <summary>
        /// Optional custom text.
        /// </summary>
        public string CustomText { get; set; }

        /// <summary>
        /// Optional secondary custom text.
        /// </summary>
        public string CustomText2 { get; set; }

        /// <summary>
        /// Optional tertiary custom text.
        /// </summary>
        public string CustomText3 { get; set; }

        /// <summary>
        /// Optional quaternary custom text.
        /// </summary>
        public string CustomText4 { get; set; }

        /// <summary>
        /// Optional quinary custom text.
        /// </summary>
        public string CustomText5 { get; set; }

        /// <summary>
        /// Default constructor.
        /// </summary>
        public ImageVisibleSignature()
        {
            ValueUri = "urn:be:e-contract:dssp:1.0:vs:si:eid-photo";
        }
    }
}
