using System;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace LinearArrayPlugin
{
    [Transaction(TransactionMode.Manual)]
    public class LinearArrayCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            try
            {
                UIDocument uidoc = commandData.Application.ActiveUIDocument;
                if (uidoc == null)
                {
                    message = "Нет активного документа.";
                    return Result.Failed;
                }

                Document doc = uidoc.Document;

                // Шаг 1: запрашиваем начальную точку
                XYZ startPoint;
                XYZ endPoint;
                try
                {
                    startPoint = uidoc.Selection.PickPoint("Укажите начальную точку");
                    endPoint = uidoc.Selection.PickPoint("Укажите конечную точку");
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    // Пользователь отменил выбор
                    return Result.Cancelled;
                }

                // Шаг 2: определяем уровень (по Z начальной точки)
                Level targetLevel = FindClosestLevel(doc, startPoint.Z);

                // Шаг 3: показываем окно с настройками
                ArraySettingsWindow window = new ArraySettingsWindow(
                    uidoc, startPoint, endPoint, targetLevel);

                window.ShowDialog();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }

        // Ищем уровень, ближайший к заданной высоте
        private Level FindClosestLevel(Document doc, double elevation)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => Math.Abs(l.Elevation - elevation))
                .FirstOrDefault();
        }
    }
}