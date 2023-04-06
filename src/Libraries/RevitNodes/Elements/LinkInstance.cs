﻿using Autodesk.Revit.DB;
using System;
using Revit.Elements;
using System.Collections.Generic;
using System.Linq;
using Dynamo.Graph.Nodes;
using DynamoUnits;
using View = Revit.Elements.Views.View;
using System.Text;
using System.Threading.Tasks;
using Level = Revit.Elements.Level;
using RevitServices.Persistence;
using Autodesk.Revit.UI;

namespace Revit.Elements
{ 
    /// <summary>
    /// A Revit Link Instance
    /// </summary>
    public static class LinkInstance
    {
        #region Action Nodes


        /// <summary>
        /// Retrieves all elements from a link instance on the given level
        /// </summary>
        /// <param name="linkInstance">Linked Element Instance </param>
        /// <param name="level">Linked Element Level </param>
        /// <returns name="elements[]">List of elements</returns>
        public static List<Revit.Elements.Element> AllElementsAtLevel(RevitLinkInstance linkInstance, Level level)
        {
            Autodesk.Revit.DB.Level revitLevel = level.InternalLevel;
            ElementId levelId = revitLevel.Id;
            ElementLevelFilter levelFilter = new ElementLevelFilter(levelId);
            var linkedAtLevel = new FilteredElementCollector(linkInstance.GetLinkDocument())
                .WhereElementIsNotElementType()
                .WherePasses(levelFilter)
                .Select(el => el.ToDSType(true))
                .ToList();
            return linkedAtLevel;
        }


        /// <summary> Retrieves all elements of a given category in a link instance </summary>
        /// <param name="linkInstance">Revit link Instance</param>
        /// <param name="category">Element category</param>
        /// <returns name="linkedElements[]">All elements of the category</returns>
        public static List<Element> AllElementsOfCategory(RevitLinkInstance linkInstance, Revit.Elements.Category category)
        {
            BuiltInCategory bic = (BuiltInCategory)System.Enum.Parse(typeof(BuiltInCategory),
                                                                                 category.InternalCategory.Id.ToString());
            var linkedElements = new FilteredElementCollector(linkInstance.GetLinkDocument())
                .OfCategory(bic)
                .WhereElementIsNotElementType()
                .ToElements()
                .Select(el => el.ToDSType(true))
                .ToList();


            return linkedElements;
        }


        /// <summary>
        /// Retrieves all elements of a category in a given view of a link instance
        /// </summary>>
        /// <param name="linkInstance">Revit link instance</param>
        /// <param name="category">Element category</param>
        /// <param name="view">View in active document</param>
        /// <returns name="linkedElementsInView[]"> All elements of the category in the view</returns>
        public static List<Revit.Elements.Element> AllElementsOfCategoryInView(RevitLinkInstance linkInstance, Elements.Category category, Elements.Views.View view)
        {

            var currentDocument = Application.Document.Current.InternalDocument;
            Autodesk.Revit.DB.View revitView = view.InternalView as Autodesk.Revit.DB.View;

            if (revitView != null && revitView.IsTemplate == false)
            {
                Solid solidForFilter;

                // When the View is a Section or Elevation, use the Crop Region
                if (revitView is ViewSection)
                {
                    if (revitView.CropBoxActive == true)
                    {
                        solidForFilter = CreateSolidFromSectionCropRegion(revitView as ViewSection);
                    }
                    else { solidForFilter = null; }
                }
                // When the View is a 3D View, use the Section Box or return null is no section box is enabled

                else if (revitView is View3D)
                {
                    View3D threeD = (View3D)revitView;

                    if (threeD.IsSectionBoxActive)
                    {
                        solidForFilter = CreateSolidFromSectionBox(revitView as View3D);
                    }
                    else { solidForFilter = null; }
                }

                else if (revitView is ViewPlan)
                {
                    if (revitView.CropBoxActive == true)
                    {
                        solidForFilter = CreateSolidFromCropRegion(currentDocument, revitView as ViewPlan);
                    }
                    else { solidForFilter = null; }
                }

                else
                { return null; }


                BuiltInCategory bic = (BuiltInCategory)System.Enum.Parse(typeof(BuiltInCategory),
                                                                                         category.InternalCategory.Id.ToString());

                if (solidForFilter != null)
                {
                    Solid transformedSolidInverted = SolidUtils.CreateTransformed(solidForFilter, linkInstance.GetTotalTransform().Inverse);
                    var solidIntersectionFilter = new Autodesk.Revit.DB.ElementIntersectsSolidFilter(transformedSolidInverted);

                    var linkedElementsInView = new FilteredElementCollector(linkInstance.GetLinkDocument())
                                    .OfCategory(bic)
                                    .WhereElementIsNotElementType()
                                    .WherePasses(solidIntersectionFilter)
                                    .ToElements()
                                    .Select(el => el.ToDSType(true))
                                    .ToList();
                    return linkedElementsInView;
                }
                else
                {
                    // for 3D views with no section box enabled - the collector will return all 
                    var linkedElements = new FilteredElementCollector(linkInstance.GetLinkDocument())
                                   .OfCategory(bic)
                                   .WhereElementIsNotElementType()
                                   .ToElements()
                                   .Select(el => el.ToDSType(true))
                                   .ToList();
                    return linkedElements;
                }


            }
            return null;
        }


        /// <summary>
        /// Retrieves all elements of a given class in a link instance
        /// </summary>
        /// <param name="linkInstance">Revit link instance</param>
        /// <param name="elementClass">Element class in the link instance</param>
        /// <returns name="elements[]">All elements of the class in the link instance</returns>
        public static List<Revit.Elements.Element> AllElementsOfClass(RevitLinkInstance linkInstance, System.Type elementClass)
        {

            var linkedOfClass = new FilteredElementCollector(linkInstance.GetLinkDocument())
                            .OfClass(elementClass)
                            .WhereElementIsNotElementType()
                            .ToElements()
                            .Select(el => el.ToDSType(true))
                            .ToList();
            return linkedOfClass;
        }

        /// <summary>
        /// Retrieves a link instance by name
        /// </summary>
        /// <param name="name">Name of the link instance</param>
        /// <returns name="linkInstance[]">Revit link instance</returns>
        public static List<RevitLinkInstance> ByName(string name)
        {
            ElementId paramId = new ElementId(BuiltInParameter.RVT_LINK_INSTANCE_NAME);
            ParameterValueProvider valueProvider = new ParameterValueProvider(paramId);
            FilterStringEquals evaluator = new FilterStringEquals();
            FilterStringRule filterStringRule = new FilterStringRule(valueProvider, evaluator, name);
            ElementParameterFilter paramterFilter = new ElementParameterFilter(filterStringRule);
            var currentDocument = Application.Document.Current.InternalDocument;
            var linkInstancesByName = new FilteredElementCollector(currentDocument)
                .OfCategory(BuiltInCategory.OST_RvtLinks)
                .WhereElementIsNotElementType()
                .WherePasses(paramterFilter)
                .Cast<RevitLinkInstance>()
                .ToList();
            return linkInstancesByName;
        }



        /// <summary>
        /// Retrieves one or more elements by ID from a link instance
        /// </summary>
        /// <param name="id">Element ID</param>
        /// <param name="linkInstance">Revit link instance</param>
        /// <returns name="element[]">Element(s) from the link instance</returns>
        public static Element ElementById(object id, RevitLinkInstance linkInstance)
        {
            int idAsInteger;
            if (id is ElementId)
            {
                idAsInteger = (id as ElementId).IntegerValue;
            }
            else
            {
                idAsInteger = int.Parse(id.ToString());
            }

            Document linkDocument = linkInstance.GetLinkDocument();
            ElementId elementId = new ElementId(idAsInteger);
            Autodesk.Revit.DB.Element linkElementById = linkDocument.GetElement((elementId));
            return linkElementById.ToDSType(true);
        }


        #endregion

        #region Query Nodes

        /// <summary>
        /// Return the Document of the given Link Instance
        /// </summary>
        /// <param name="linkInstance"> Link Instance </param>
        /// <returns name="linkDocument">linkDocument</returns>
        [NodeCategory("Query")]
        public static Revit.Application.Document Document(RevitLinkInstance linkInstance)
        {

            var linkDocument = linkInstance.GetLinkDocument();

            return new Revit.Application.Document(linkDocument);
        }



        /// <summary>
        /// Retrieves a link instance’s GUID
        /// </summary>      
        /// <param name="linkInstance">Revit link instance</param>
        /// <returns name="string[]">GUID of link instance</returns>
        [NodeCategory("Query")]
        public static string UniqueId(RevitLinkInstance linkInstance)
        {
            string uniqueId = linkInstance.UniqueId;
            return uniqueId;
        }

        #endregion


        #region Helpers


        private static Solid CreateSolidFromCropRegion(Document doc, Autodesk.Revit.DB.ViewPlan viewPlan)
        {

            PlanViewRange planViewRange = viewPlan.GetViewRange();
            ViewType viewType = viewPlan.ViewType;
            ElementId cutPlaneLevelId = planViewRange.GetLevelId(PlanViewPlane.CutPlane);
            ElementId viewDepthLevelId = planViewRange.GetLevelId(PlanViewPlane.ViewDepthPlane);
            Autodesk.Revit.DB.Level cutPlaneLevel = doc.GetElement(cutPlaneLevelId) as Autodesk.Revit.DB.Level;
            double cutPlaneLevelElevation = cutPlaneLevel.Elevation;
            Autodesk.Revit.DB.Level viewDepthPlaneLevel = doc.GetElement(viewDepthLevelId) as Autodesk.Revit.DB.Level;

            double viewDepthPlaneLevelElevation;
            // check if the View Depth is set to a Level, in which case get the level elevation
            if (viewDepthPlaneLevel != null)
            {
                viewDepthPlaneLevelElevation = viewDepthPlaneLevel.Elevation;
            }
            // if View Depth is set to Unlimited or Level Below (where no level below exists), use the Bounding Box Max (for RCP) or Min (for downward-looking plan views)
            else
            {
                if (viewType == ViewType.CeilingPlan)
                {
                    viewDepthPlaneLevelElevation = viewPlan.get_BoundingBox(null).Max.Z;
                }
                else
                {
                    viewDepthPlaneLevelElevation = viewPlan.get_BoundingBox(null).Min.Z;
                }
            }
            double cutPlaneOffset = planViewRange.GetOffset(PlanViewPlane.CutPlane);
            double viewDepthOffset = planViewRange.GetOffset(PlanViewPlane.ViewDepthPlane);


            double solidBottomZ;
            double solidTopZ;

            if (viewType == ViewType.CeilingPlan)
            {
                solidBottomZ = cutPlaneLevelElevation + cutPlaneOffset;
                solidTopZ = viewDepthPlaneLevelElevation + viewDepthOffset;
            }
            else
            {
                solidBottomZ = viewDepthPlaneLevelElevation + viewDepthOffset;
                solidTopZ = cutPlaneLevelElevation + cutPlaneOffset;
            }

            double solidHeight = solidTopZ - solidBottomZ;

            // using crop region to generate the solid for intersection
            ViewCropRegionShapeManager crsm = viewPlan.GetCropRegionShapeManager();
            IList<CurveLoop> cropLoopList = crsm.GetCropShape();

            CurveLoop correctedCropLoop = new CurveLoop();

            foreach (CurveLoop cropLoop in cropLoopList)
            {
                foreach (Curve curve in cropLoop)
                {
                    XYZ startPoint = curve.GetEndPoint(0);
                    XYZ endPoint = curve.GetEndPoint(1);
                    XYZ correctedStartPoint = new XYZ(startPoint.X, startPoint.Y, solidBottomZ);
                    XYZ correctedEndPoint = new XYZ(endPoint.X, endPoint.Y, solidBottomZ);
                    Line line = Line.CreateBound(correctedStartPoint, correctedEndPoint);
                    correctedCropLoop.Append(line);
                }
            }

            List<CurveLoop> correctedCropLoopList = new List<CurveLoop>
            {
                correctedCropLoop
            };

            XYZ direction = XYZ.BasisZ;
            Solid cropViewSolid = GeometryCreationUtilities.CreateExtrusionGeometry(correctedCropLoopList, direction, solidHeight);

            return cropViewSolid;
        }


        private static Solid CreateSolidFromSectionCropRegion(ViewSection viewSection)
        {
            XYZ sectionDirection = viewSection.ViewDirection.Negate();
            BoundingBoxXYZ sectionBBox = viewSection.get_BoundingBox(null);
            double extrusionDistance = sectionBBox.Max.Z - sectionBBox.Min.Z;

            ViewCropRegionShapeManager crsm = viewSection.GetCropRegionShapeManager();
            IList<CurveLoop> cropLoopList = crsm.GetCropShape();

            Solid cropSolid = GeometryCreationUtilities.CreateExtrusionGeometry(cropLoopList, sectionDirection, extrusionDistance);

            return cropSolid;
        }


        private static Solid CreateSolidFromSectionBox(Autodesk.Revit.DB.View3D view3D)
        {
            BoundingBoxXYZ bBox = view3D.GetSectionBox();
            Transform bBoxTransform = bBox.Transform;

            // recontstruct solid by Bounding Box corners 
            XYZ pt0 = new XYZ(bBox.Min.X, bBox.Min.Y, bBox.Min.Z);
            XYZ pt1 = new XYZ(bBox.Max.X, bBox.Min.Y, bBox.Min.Z);
            XYZ pt2 = new XYZ(bBox.Max.X, bBox.Max.Y, bBox.Min.Z);
            XYZ pt3 = new XYZ(bBox.Min.X, bBox.Max.Y, bBox.Min.Z);

            Line edge0 = Line.CreateBound(pt0, pt1);
            Line edge1 = Line.CreateBound(pt1, pt2);
            Line edge2 = Line.CreateBound(pt2, pt3);
            Line edge3 = Line.CreateBound(pt3, pt0);

            List<Curve> edges = new List<Curve>
            {
                edge0,
                edge1,
                edge2,
                edge3
            };

            double height = bBox.Max.Z - bBox.Min.Z;
            CurveLoop curveLoop = CurveLoop.Create(edges);
            List<CurveLoop> curveLoopList = new List<CurveLoop>
            {
                curveLoop
            };

            XYZ direction = XYZ.BasisZ;
            // transform for positioning the solid within the current doc
            Solid preTransformSolid = GeometryCreationUtilities.CreateExtrusionGeometry(curveLoopList, direction, height);
            Solid sectionBoxSolid = SolidUtils.CreateTransformed(preTransformSolid, bBoxTransform);
            return sectionBoxSolid;
        }

        #endregion

       
        /// <summary>
        /// Returns the link instances by the provided Document Title (revit file name)
        /// </summary>
        /// <param name="title">The Title of the Link Document (the revit file name without the extension) </param>
        /// <returns name="linkInstance">Revil link instance</returns>
        public static List<RevitLinkInstance> ByTitle(string title)
        {
            var currentDocument = Application.Document.Current.InternalDocument;
            var linkInstances = new FilteredElementCollector(currentDocument)
                .OfCategory(BuiltInCategory.OST_RvtLinks)
                .WhereElementIsNotElementType()            
                .ToList();
            List<RevitLinkInstance> linkInstancesByTitle = new List<RevitLinkInstance>();
            foreach (RevitLinkInstance linkInstance in linkInstances)
                {
                Document linkDoc = linkInstance.GetLinkDocument();
                if (linkDoc.Title == title)
                    { 
                        linkInstancesByTitle.Add(linkInstance);
                    }
                }
            return linkInstancesByTitle;
        }





    }
}
