﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Dynamo.Interfaces;
using Dynamo.ViewModels;

using ProtoCore.Mirror;
using RevitServices.Persistence;
using Element = Revit.Elements.Element;

namespace Dynamo.Applications
{
    /// <summary>
    ///     An Revit-specific implementation of IWatchHandler that is set on the DynamoViewModel at startup.
    ///     The main GenerateWatchViewModelForData method dynamically dispatches to the appropriate
    ///     internal method based on the type. For every time for which you would like
    ///     to have a custom representation in the watch, you will need an additional
    ///     method on this handler
    /// </summary>
    public class RevitWatchHandler : IWatchHandler
    {
        private readonly IWatchHandler baseHandler;
        private readonly IPreferences preferences;

        public RevitWatchHandler(IPreferences prefs)
        {
            baseHandler = new DefaultWatchHandler(prefs);
            preferences = prefs;
        }

        private WatchViewModel ProcessThing(Element element, List<string> preferredDictionaryOrdering, ProtoCore.RuntimeCore runtimeCore, string tag, bool showRawData, WatchHandlerCallback callback)
        {
            var id = element.Id;

            var node = new WatchViewModel(element.ToString(preferences.NumberFormat, CultureInfo.InvariantCulture), tag, RequestSelectGeometry);

            node.Clicked += () =>
            {
                // added for linked elements
                Document elementDoc = element.InternalElement.Document;
                Document currrentDoc = Revit.Application.Document.Current.InternalDocument;
                if (!(elementDoc.Equals(currrentDoc)) && (element.InternalElement.IsValidObject))
                {
                    ZoomToLinkedElement(element);
                }
                else if (element.InternalElement.IsValidObject)
                    DocumentManager.Instance.CurrentUIDocument.ShowElements(element.InternalElement);
            };

            node.Link = id.ToString(CultureInfo.InvariantCulture);

            return node;
        }

        private static void ZoomToLinkedElement(Element element)
        {

            double zoomOffset = 4;
            UIDocument uiDoc = DocumentManager.Instance.CurrentUIDocument;
            View activeView = uiDoc.ActiveView;
            // get active UI view to use
            UIView uiview = uiDoc.GetOpenUIViews().FirstOrDefault<UIView>(uv => uv.ViewId.Equals(activeView.Id));
            var location = Revit.Elements.LinkElement.GetLinkElementLocation(element);
            Transform linkTransform = Revit.Elements.LinkElement.LinkTransform(element);
            XYZ locationPt = new XYZ();
            if (location is XYZ)
            {
                locationPt = location as XYZ;
            }
            else if (location is Autodesk.Revit.DB.Curve)
                locationPt = (location as Curve).Evaluate(0.5, true);
            else
            {
                BoundingBoxXYZ bb = element.InternalElement.get_BoundingBox(null);
                XYZ bbCenter = (bb.Max + bb.Min) / 2;
                locationPt = Revit.Elements.LinkElement.TransformPoint(bbCenter,linkTransform);

            }
            if (locationPt != null)
            {
                XYZ min = new XYZ(locationPt.X - zoomOffset, locationPt.Y - zoomOffset, locationPt.Z - zoomOffset);
                XYZ max = new XYZ(locationPt.X + zoomOffset, locationPt.Y + zoomOffset, locationPt.Z + zoomOffset);          
                uiview.ZoomAndCenterRectangle(min, max);
            }
            
           
        }

        //If no dispatch target is found, then invoke base watch handler.
        private WatchViewModel ProcessThing(object obj, List<string> preferredDictionaryOrdering, ProtoCore.RuntimeCore runtimeCore, string tag, bool showRawData, WatchHandlerCallback callback)
        {
            return baseHandler.Process(obj, preferredDictionaryOrdering, runtimeCore, tag, showRawData, callback);
        }

        private WatchViewModel ProcessThing(MirrorData data, List<string> preferredDictionaryOrdering, ProtoCore.RuntimeCore runtimeCore, string tag, bool showRawData, WatchHandlerCallback callback)
        {
            try
            {
                return baseHandler.Process(data, preferredDictionaryOrdering, runtimeCore, tag, showRawData, callback);
            }
            catch (Exception)
            {
                return callback(data.Data, preferredDictionaryOrdering, runtimeCore, tag, showRawData);
            }
        }

        public WatchViewModel Process(dynamic value, IEnumerable<string> preferredDictionaryOrdering, ProtoCore.RuntimeCore runtimeCore, string tag, bool showRawData, WatchHandlerCallback callback)
        {
            return Object.ReferenceEquals(value, null)
                ? new WatchViewModel("null", tag, RequestSelectGeometry)
                : ProcessThing(value, preferredDictionaryOrdering?.ToList(), runtimeCore, tag, showRawData, callback);
        }


        public event Action<string> RequestSelectGeometry;
    }
}
