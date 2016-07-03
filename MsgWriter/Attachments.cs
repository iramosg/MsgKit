﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MsgWriter.Exceptions;
using MsgWriter.Helpers;
using MsgWriter.Streams;
using MsgWriter.Structures;
using OpenMcdf;

/*
   Copyright 2015 - 2016 Kees van Spelde

   Licensed under The Code Project Open License (CPOL) 1.02;
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

     http://www.codeproject.com/info/cpol10.aspx

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
*/

namespace MsgWriter
{
    #region Enum AttachmentType
    /// <summary>
    ///     The type of the attachment
    /// </summary>
    public enum AttachmentType : uint
    {
        /// <summary>
        ///     There is no attachment
        /// </summary>
        NoAttachment = 0x0000,

        /// <summary>
        ///     The  <see cref="PropertyTags.PR_ATTACH_DATA_BIN" /> property contains the attachment data
        /// </summary>
        AttachByValue = 0x0001,

        /// <summary>
        ///     The <see cref="PropertyTags.PR_ATTACH_PATHNAME_W" /> or <see cref="PropertyTags.PR_ATTACH_LONG_PATHNAME_W" />
        ///     property contains a fully qualified path identifying the attachment to recipients with access to a common file server
        /// </summary>
        AttachByReference = 0x0002,

        /// <summary>
        ///     The <see cref="PropertyTags.PR_ATTACH_PATHNAME_W" /> or <see cref="PropertyTags.PR_ATTACH_LONG_PATHNAME_W" />
        ///     property contains a fully qualified path identifying the attachment
        /// </summary>
        AttachByRefResolve = 0x0003,

        /// <summary>
        ///     The <see cref="PropertyTags.PR_ATTACH_PATHNAME_W" /> or <see cref="PropertyTags.PR_ATTACH_LONG_PATHNAME_W" />
        ///     property contains a fully qualified path identifying the attachment
        /// </summary>
        AttachByRefOnly = 0x0004,

        /// <summary>
        ///     The  <see cref="PropertyTags.PR_ATTACH_DATA_OBJ" /> (PidTagAttachDataObject) property contains an embedded object
        ///     that supports the IMessage interface
        /// </summary>
        AttachEmbeddedMsg = 0x0005,

        /// <summary>
        ///     The attachment is an embedded OLE object
        /// </summary>
        AttachOle = 0x0006
    }
    #endregion

    /// <summary>
    ///     Contains a list of <see cref="Attachment" /> objects that are added to a <see cref="Message" />
    /// </summary>
    /// <remarks>
    ///     See https://msdn.microsoft.com/en-us/library/office/cc842285.aspx
    /// </remarks>
    public sealed class Attachments : List<Attachment>
    {
        #region CheckAttachmentFileName
        /// <summary>
        ///     Checks if the <paramref name="fileName" /> already exists in this object
        /// </summary>
        /// <param name="fileName"></param>
        private void CheckAttachmentFileName(string fileName)
        {
            var file = Path.GetFileName(fileName);

            if (this.Any(
                attachment => attachment.FileName.Equals(fileName, StringComparison.InvariantCultureIgnoreCase)))
                throw new MWAttachmentExists("The attachment with the name '" + file + "' already exists");
        }
        #endregion

        #region WriteToStorage
        /// <summary>
        ///     Writes the <see cref="Attachment" /> objects to the given <paramref name="rootStorage" />
        ///     and it will set all the needed properties
        /// </summary>
        /// <param name="rootStorage">The root <see cref="CFStorage" /></param>
        internal void WriteToStorage(CFStorage rootStorage)
        {
            for (var index = 0; index < Count; index++)
            {
                var attachment = this[index];
                var storage =
                    rootStorage.AddStorage(PropertyTags.AttachmentStoragePrefix + index.ToString("X8").ToUpper());
                attachment.WriteProperties(storage, index);
            }
        }
        #endregion

        #region AddAttachment
        /// <summary>
        ///     Add's an <see cref="Attachment" /> by <see cref="AttachmentType.AttachByValue" /> (default)
        /// </summary>
        /// <param name="fileName">The file to add with full path</param>
        /// <param name="isInline">Set to true to add the attachment inline</param>
        /// <param name="contentId">The id for the inline attachment when <paramref name="isInline" /> is set to true</param>
        /// <exception cref="FileNotFoundException">Raised when the <paramref name="fileName" /> could not be found</exception>
        /// <exception cref="MWAttachmentExists">Raised when an attachment with the same name already exists</exception>
        /// <exception cref="ArgumentNullException">
        ///     Raised when <paramref name="isInline" /> is set to true and
        ///     <paramref name="contentId" /> is null, white space or empty
        /// </exception>
        public void AddAttachment(string fileName, bool isInline = false, string contentId = "")
        {
            CheckAttachmentFileName(fileName);
            var file = new FileInfo(fileName);
            Add(new Attachment(file.OpenRead(),
                fileName,
                file.CreationTime,
                file.LastAccessTime,
                AttachmentType.AttachByValue,
                isInline,
                contentId));
        }

        /// <summary>
        ///     Add's an <see cref="Attachment" /> by <see cref="AttachmentType.AttachByValue" /> (default)
        /// </summary>
        /// <param name="stream">The stream to the attachment</param>
        /// <param name="fileName">The name for the attachment</param>
        /// <param name="isInline">Set to true to add the attachment inline</param>
        /// <param name="contentId">The id for the inline attachment when <paramref name="isInline" /> is set to true</param>
        /// <exception cref="ArgumentNullException">Raised when the stream is null</exception>
        /// <exception cref="MWAttachmentExists">Raised when an attachment with the same name already exists</exception>
        /// <exception cref="ArgumentNullException">
        ///     Raised when <paramref name="isInline" /> is set to true and
        ///     <paramref name="contentId" /> is null, white space or empty
        /// </exception>
        public void AddAttachment(Stream stream, string fileName, bool isInline = false, string contentId = "")
        {
            if (stream == null)
                throw new ArgumentNullException("stream");

            CheckAttachmentFileName(fileName);
            var dateTime = DateTime.Now;
            Add(new Attachment(stream,
                fileName,
                dateTime,
                dateTime,
                AttachmentType.AttachByValue,
                isInline,
                contentId));
        }
        #endregion
    }

    /// <summary>
    ///     This class represents an attachment
    /// </summary>
    public sealed class Attachment
    {
        #region Properties
        /// <summary>
        ///     The stream to the attachment
        /// </summary>
        public Stream Stream { get; }

        /// <summary>
        ///     The filename of the attachment
        /// </summary>
        public string FileName { get; }

        /// <summary>
        ///     The <see cref="AttachmentType"/>
        /// </summary>
        public AttachmentType Type { get; }

        /// <summary>
        ///     The content id for an inline attachment
        /// </summary>
        public string ContentId { get; private set; }

        /// <summary>
        ///     True when the attachment is inline
        /// </summary>
        public bool IsInline { get; private set; }

        /// <summary>
        ///     Tthe date and time when the attachment was created
        /// </summary>
        public DateTime CreationTime { get; private set; }

        /// <summary>
        ///     The date and time when the attachment was last modified
        /// </summary>
        public DateTime LastModificationTime { get; private set; }
        #endregion

        #region Constructor
        /// <summary>
        ///     Creates a new attachment object and sets all its properties
        /// </summary>
        /// <param name="stream">The stream to the attachment</param>
        /// <param name="fileName">The attachment filename</param>
        /// <param name="creationTime">The date and time when the attachment was created</param>
        /// <param name="lastModificationTime">The date and time when the attachment was last modified</param>
        /// <param name="type">The <see cref="AttachmentType"/></param>
        /// <param name="isInline">True when the attachment is inline</param>
        /// <param name="contentId">The id for the attachment when <paramref name="isInline" /> is set to true</param>
        /// <exception cref="ArgumentNullException">
        ///     Raised when <paramref name="isInline" /> is set to true and
        ///     <paramref name="contentId" /> is null, white space or empty
        /// </exception>
        internal Attachment(Stream stream,
            string fileName,
            DateTime creationTime,
            DateTime lastModificationTime,
            AttachmentType type = AttachmentType.AttachByValue,
            bool isInline = false,
            string contentId = "")
        {
            Stream = stream;
            FileName = Path.GetFileName(fileName);
            CreationTime = creationTime;
            LastModificationTime = lastModificationTime;
            Type = type;
            IsInline = isInline;
            ContentId = contentId;

            if (isInline && string.IsNullOrWhiteSpace(contentId))
                throw new ArgumentNullException("contentId", "The content id cannot be empty when isInline is set to true");
        }
        #endregion

        #region WriteProperties
        /// <summary>
        ///     Writes all the string and binary <see cref="Property">properties</see> as a <see cref="CFStream" /> to the
        ///     given <paramref name="storage" />
        /// </summary>
        /// <param name="storage">The <see cref="CFStorage" /></param>
        /// <param name="recordKey">The record key</param>
        internal void WriteProperties(CFStorage storage, int recordKey)
        {
            var propertiesStream = new AttachmentProperties();
            propertiesStream.AddProperty(PropertyTags.PR_RECORD_KEY, recordKey);
            propertiesStream.AddProperty(PropertyTags.PR_DISPLAY_NAME_W, FileName);

            if (!string.IsNullOrEmpty(FileName))
                propertiesStream.AddProperty(PropertyTags.PR_ATTACH_EXTENSION_W, Path.GetExtension(FileName));

            propertiesStream.AddProperty(PropertyTags.PR_ATTACH_DATA_BIN, Stream.ToByteArray());
            propertiesStream.AddProperty(PropertyTags.PR_ATTACH_METHOD, AttachmentType.AttachByValue);
            propertiesStream.AddProperty(PropertyTags.PR_ATTACH_SIZE, Stream.Length);

            var utcNow = DateTime.Now;
            propertiesStream.AddProperty(PropertyTags.PR_CREATION_TIME, utcNow);
            propertiesStream.AddProperty(PropertyTags.PR_LAST_MODIFICATION_TIME, utcNow);

            propertiesStream.WriteProperties(storage);
        }
        #endregion
    }
}