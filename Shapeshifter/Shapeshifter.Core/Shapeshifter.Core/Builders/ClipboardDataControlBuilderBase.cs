﻿using Shapeshifter.Core.Helpers;
using Shapeshifter.Core.Strategies;
using System;
using Autofac;
using Shapeshifter.Core.Data;

namespace Shapeshifter.Core.Builders
{
    public abstract class ClipboardDataControlBuilderBase<TControlType, TDataType> : IClipboardDataControlBuilder<IClipboardItemControl<TControlType>, TDataType>
        where TDataType : IClipboardData
    {
        protected abstract IControlConstructionStrategy<TControlType, TDataType> HeaderBuildingStrategy { get; }
        protected abstract IControlConstructionStrategy<TControlType, TDataType> BodyBuildingStrategy { get; }
        protected abstract IControlConstructionStrategy<TControlType, TDataType> SourceBuildingStrategy { get; }
        protected abstract IControlConstructionStrategy<TControlType, TDataType> BackgroundImageBuildingStrategy { get; }

        protected ClipboardDataControlBuilderBase()
        {
            //constructor of an abstract class should be protected.
        }

        public IClipboardItemControl<TControlType> Build(TDataType data)
        {
            if (!CanBuild(data))
            {
                throw new InvalidOperationException("Data format not supported.");
            }

            if (HeaderBuildingStrategy == null || BodyBuildingStrategy == null || SourceBuildingStrategy == null)
            {
                throw new InvalidOperationException("Both the header, source and body building strategies must be available before calling Build.");
            }

            var header = HeaderBuildingStrategy.ConstructControl(data);
            var body = BodyBuildingStrategy.ConstructControl(data);
            var source = SourceBuildingStrategy.ConstructControl(data);

            var container = InversionOfControlHelper.Container;

            var newControl = container.Resolve<IClipboardItemControl<TControlType>>();

            newControl.Header = header;
            newControl.Body = body;
            newControl.Source = source;

            if (BackgroundImageBuildingStrategy != null)
            {
                var backgroundImage = BackgroundImageBuildingStrategy.ConstructControl(data);
                newControl.BackgroundImage = backgroundImage;
            }

            return newControl;
        }

        //TODO: this should actually be able to determine whether or not something can be built through reflection.
        public abstract bool CanBuild(IClipboardData data);
    }
}
