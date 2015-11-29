﻿namespace Shapeshifter.Tests.Controls.Clipboard.Factories
{
    using System;

    using Autofac;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using UserInterface.WindowsDesktop.Controls.Clipboard.Factories.Interfaces;
    using UserInterface.WindowsDesktop.Controls.Clipboard.Interfaces;
    using UserInterface.WindowsDesktop.Data.Interfaces;

    [TestClass]
    public class ClipboardFileDataControlFactoryTest: TestBase
    {
        [TestMethod]
        [ExpectedException(typeof (ArgumentException))]
        public void CreateControlWithNoDataThrowsException()
        {
            var container = CreateContainer();

            var factory =
                container
                    .Resolve
                    <IClipboardControlFactory<IClipboardFileData, IClipboardFileDataControl>>();
            factory.CreateControl(null);
        }
    }
}