﻿namespace Shapeshifter.UserInterface.WindowsDesktop.Controls.Clipboard.Designer.Services
{
    using System.Diagnostics.CodeAnalysis;

    using WindowsDesktop.Services.Files.Interfaces;

    using Interfaces;

    [ExcludeFromCodeCoverage]
    class DesignerFileManager
        : IFileManager,
          IDesignerService
    {
        public string PrepareFolder(string path)
        {
            return null;
        }

        public string WriteBytesToTemporaryFile(string path, byte[] bytes)
        {
            return null;
        }
    }
}