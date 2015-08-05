﻿using System;
using System.Collections.Generic;
using Shapeshifter.UserInterface.WindowsDesktop.Core.Data;
using Shapeshifter.UserInterface.WindowsDesktop.Data.Interfaces;
using Shapeshifter.UserInterface.WindowsDesktop.Factories.Interfaces;
using Shapeshifter.UserInterface.WindowsDesktop.Services.Events;
using Shapeshifter.UserInterface.WindowsDesktop.Services.Interfaces;

namespace Shapeshifter.UserInterface.WindowsDesktop.Services
{
    class ClipboardUserInterfaceMediator : IClipboardUserInterfaceMediator
    {
        private readonly IClipboardHookService clipboardHook;
        private readonly IKeyboardHookService keyboardHook;

        private readonly IEnumerable<IClipboardDataControlFactory> dataFactories;
        private readonly IList<IClipboardControlDataPackage> clipboardPackages;

        public event EventHandler<ControlEventArgument> ControlAdded;
        public event EventHandler<ControlEventArgument> ControlRemoved;
        public event EventHandler<ControlEventArgument> ControlPinned;
        public event EventHandler<ControlEventArgument> ControlHighlighted;

        public bool IsConnected
        {
            get
            {
                return clipboardHook.IsConnected || keyboardHook.IsConnected;
            }
        }

        public ClipboardUserInterfaceMediator(
            IEnumerable<IClipboardDataControlFactory> dataFactories, 
            IClipboardHookService clipboardHook, 
            IKeyboardHookService keyboardHook)
        {
            this.dataFactories = dataFactories;

            clipboardPackages = new List<IClipboardControlDataPackage>();
            
            this.clipboardHook = clipboardHook;
            this.keyboardHook = keyboardHook;
        }

        private void ClipboardHook_DataCopied(object sender, Events.DataCopiedEventArgument e)
        {
            var dataObject = e.Data;

            var package = new ClipboardDataControlPackage();
            foreach (var factory in dataFactories)
            {
                foreach (var format in dataObject.GetFormats(true))
                {
                    if (factory.CanBuildData(format))
                    {
                        var rawData = dataObject.GetData(format);

                        var clipboardData = factory.BuildData(format, rawData);
                        package.AddData(clipboardData);
                    }
                }
            }

            //construct a control if the package contains any data.
            foreach (var factory in dataFactories)
            {
                foreach (var data in package.Contents)
                {
                    if (factory.CanBuildControl(data))
                    {
                        package.Control = factory.BuildControl(data);
                        break;
                    }
                }
            }

            if(package.Control == null)
            {
                throw new NotImplementedException("Can't handle unknown data formats yet.");
            }
            
            //signal an added event.
            if(ControlAdded != null)
            {
                ControlAdded(this, new ControlEventArgument(package));
            }
        }

        public void Disconnect()
        {
            clipboardHook.DataCopied -= ClipboardHook_DataCopied;

            keyboardHook.Disconnect();
            clipboardHook.Disconnect();
        }

        public void Connect()
        {
            keyboardHook.Connect();
            clipboardHook.Connect();

            clipboardHook.DataCopied += ClipboardHook_DataCopied;
        }

        public IEnumerable<IClipboardControlDataPackage> ClipboardElements
        {
            get
            {
                return clipboardPackages;
            }
        }
    }
}
