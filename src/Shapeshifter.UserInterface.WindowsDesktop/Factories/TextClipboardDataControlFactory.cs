﻿namespace Shapeshifter.UserInterface.WindowsDesktop.Factories
{
    using System;
    using System.Text;

    using Api;

    using Controls.Clipboard.Factories.Interfaces;
    using Controls.Clipboard.Interfaces;

    using Data;
    using Data.Interfaces;

    using Interfaces;

    using Services.Interfaces;

    class TextClipboardDataControlFactory: ITextClipboardDataControlFactory
    {
        readonly IDataSourceService dataSourceService;

        readonly IClipboardControlFactory<IClipboardTextData, IClipboardTextDataControl>
            textControlFactory;

        public TextClipboardDataControlFactory(
            IDataSourceService dataSourceService,
            IClipboardControlFactory<IClipboardTextData, IClipboardTextDataControl>
                textControlFactory)
        {
            this.dataSourceService = dataSourceService;
            this.textControlFactory = textControlFactory;
        }

        public IClipboardControl BuildControl(IClipboardData clipboardData)
        {
            return textControlFactory.CreateControl((IClipboardTextData) clipboardData);
        }

        public IClipboardData BuildData(uint format, byte[] data)
        {
            if (!CanBuildData(format))
            {
                throw new ArgumentException(
                    "Can't construct data from this format.",
                    nameof(format));
            }

            var text = GetProcessedTextFromRawData(format, data);
            return new ClipboardTextData(dataSourceService)
            {
                Text = text,
                RawData = data,
                RawFormat = format
            };
        }

        static string GetProcessedTextFromRawData(uint format, byte[] data)
        {
            var text = GetTextFromRawData(format, data);

            var terminaryNullCharacterPosition = text.IndexOf('\0');
            if (terminaryNullCharacterPosition > -1)
            {
                return text.Substring(0, terminaryNullCharacterPosition);
            }
            return text;
        }

        static string GetTextFromRawData(uint format, byte[] data)
        {
            switch (format)
            {
                case ClipboardApi.CF_TEXT:
                    return Encoding.UTF8.GetString(data);

                case ClipboardApi.CF_OEMTEXT:
                    return Encoding.Default.GetString(data);

                case ClipboardApi.CF_UNICODETEXT:
                    return Encoding.Unicode.GetString(data);

                default:
                    throw new InvalidOperationException("Unknown format.");
            }
        }

        public bool CanBuildControl(IClipboardData data)
        {
            return
                data is IClipboardTextData;
        }

        public bool CanBuildData(uint format)
        {
            return
                format == ClipboardApi.CF_TEXT ||
                format == ClipboardApi.CF_OEMTEXT ||
                format == ClipboardApi.CF_UNICODETEXT;
        }
    }
}