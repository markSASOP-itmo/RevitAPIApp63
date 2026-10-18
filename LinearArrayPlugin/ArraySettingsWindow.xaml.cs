using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace LinearArrayPlugin
{
    public partial class ArraySettingsWindow : Window
    {
        private readonly UIDocument _uidoc;
        private readonly Document _doc;
        private readonly XYZ _startPoint;
        private readonly XYZ _endPoint;
        private readonly Level _targetLevel;

        // Вспомогательный класс для отображения в ComboBox
        public class FamilyTypeItem
        {
            public FamilySymbol Symbol { get; set; }
            public string DisplayName => $"{Symbol.Family.Name} : {Symbol.Name}";
        }

        public ArraySettingsWindow(
            UIDocument uidoc,
            XYZ startPoint,
            XYZ endPoint,
            Level targetLevel)
        {
            InitializeComponent();

            _uidoc = uidoc;
            _doc = uidoc.Document;
            _startPoint = startPoint;
            _endPoint = endPoint;
            _targetLevel = targetLevel;

            // Показываем длину между точками
            double distanceFeet = _startPoint.DistanceTo(_endPoint);
            double distanceMm = UnitUtils.ConvertFromInternalUnits(distanceFeet, UnitTypeId.Millimeters);
            DistanceInfoTextBlock.Text = $"Длина между точками: {distanceMm:F1} мм";

            LoadFamilyTypes();
        }

        // Загружаем все загруженные типы семейств
        private void LoadFamilyTypes()
        {
            var types = new FilteredElementCollector(_doc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .Where(fs => fs.Category != null) // исключаем служебные
                .OrderBy(fs => fs.Category.Name)
                .ThenBy(fs => fs.Family.Name)
                .ThenBy(fs => fs.Name)
                .Select(fs => new FamilyTypeItem { Symbol = fs })
                .ToList();

            FamilyTypeComboBox.ItemsSource = types;
            FamilyTypeComboBox.SelectedIndex = types.Count > 0 ? 0 : -1;

            if (types.Count == 0)
            {
                MessageBox.Show(
                    "В проекте не найдено загруженных типов семейств.\n" +
                    "Загрузите хотя бы одно семейство.",
                    "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void PlaceButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Проверяем выбор
                FamilyTypeItem selectedItem = FamilyTypeComboBox.SelectedItem as FamilyTypeItem;
                if (selectedItem == null)
                {
                    MessageBox.Show("Выберите тип семейства.", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Парсим количество
                if (!int.TryParse(CountTextBox.Text, out int count))
                {
                    MessageBox.Show("Введите целое число элементов.", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (count < 2)
                {
                    MessageBox.Show("Количество элементов должно быть не меньше 2.",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                FamilySymbol symbol = selectedItem.Symbol;

                // Если точки совпадают — распределить нельзя
                if (_startPoint.DistanceTo(_endPoint) < 1e-6)
                {
                    MessageBox.Show("Начальная и конечная точки совпадают.",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Создаём элементы
                using (Transaction trans = new Transaction(_doc, "Расстановка элементов"))
                {
                    trans.Start();

                    // Активируем тип, если нужно
                    if (!symbol.IsActive)
                    {
                        symbol.Activate();
                        _doc.Regenerate();
                    }

                    // Вектор направления и шаг
                    XYZ direction = _endPoint - _startPoint;
                    XYZ step = direction / (count - 1);

                    // Создаём элементы
                    for (int i = 0; i < count; i++)
                    {
                        XYZ point = _startPoint + step * i;

                        // Корректируем Z по уровню (если он задан)
                        if (_targetLevel != null)
                        {
                            point = new XYZ(point.X, point.Y, _targetLevel.Elevation);
                        }

                        _doc.Create.NewFamilyInstance(
                            point,
                            symbol,
                            _targetLevel,
                            Autodesk.Revit.DB.Structure.StructuralType.NonStructural
                        );
                    }

                    trans.Commit();
                }

                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при расстановке:\n{ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}