using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace EseView
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private MainViewModel m_viewModel;
        private string m_filename;

        public MainWindow()
        {
            m_viewModel = new MainViewModel();
            InitializeComponent();

            TableList.DataContext = m_viewModel;
            TableList.SelectionChanged += TableList_SelectionChanged;

            StatusText.Text = "No database loaded.";
        }

        void TableList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TableList.SelectedIndex == -1)
            {
                RowGrid.Columns.Clear();
                RowData.DataContext = null;
                return;
            }

            string tableName = TableList.SelectedItem as string;

            RowGrid.Columns.Clear();

            foreach (KeyValuePair<string, Type> colspec in m_viewModel.GetColumnNamesAndTypes(tableName))
            {
                var cellBinding = new Binding();
                cellBinding.Converter = new DBRowValueConverter();
                cellBinding.ConverterParameter = colspec.Key;

                // TODO: use different cell element types based on the underlying data type.
                var cellFactory = new FrameworkElementFactory(typeof(ContentControl));
                cellFactory.SetBinding(ContentControl.ContentProperty, cellBinding);

                var template = new DataTemplate();
                template.VisualTree = cellFactory;

                var gridColumn2 = new GridViewColumn();
                gridColumn2.Header = colspec.Key;
                gridColumn2.CellTemplate = template;

                RowGrid.Columns.Add(gridColumn2);
            }

            RowData.DataContext = m_viewModel.VirtualRows(tableName);
            UpdateStatusText(tableName);
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog();
            bool? result = dialog.ShowDialog();

            if (result == true)
            {
                m_filename = dialog.FileName;
                m_viewModel.OpenDatabase(dialog.FileName);

                TableList.DataContext = m_viewModel.Tables;
                TableList.SelectedIndex = -1;
            }

            Title = "EseView: " + m_filename;
            UpdateStatusText(null);
        }

        private void UpdateStatusText(string tableName)
        {
            string text = string.Format("{0} tables.", m_viewModel.Tables.Count);
            if (!string.IsNullOrEmpty(tableName))
            {
                text += string.Format(" {0} rows in current table.", m_viewModel.GetRowCount(tableName));
            }
            StatusText.Text = text;
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("EseView by Bill Fraser <wfraser@microsoft.com>", "About EseView", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
