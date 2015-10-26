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
        private string m_selectedIndex;
        private string m_selectedTable;

        private enum Mode
        {
            Data,
            IndexInfo,
            ColumnInfo
        }

        private Mode m_mode;

        public MainWindow()
        {
            m_selectedTable = null;
            m_selectedIndex = null;
            m_mode = Mode.Data;

            m_viewModel = new MainViewModel();
            InitializeComponent();

            TableList.DataContext = m_viewModel;
            TableList.SelectionChanged += TableList_SelectionChanged;

            StatusText.Text = "No database loaded.";

            var args = Environment.GetCommandLineArgs();
            if (args.Length == 2)
            {
                OpenDatabase(args[1]);
            }
            else if (args.Length > 1)
            {
                MessageBox.Show("Invalid command line arguments.\n"
                    + "usage: EseView.exe [database filename]",
                    "Invalid command line arguments",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        void UpdateIndexList()
        {
            IndexSelector.Items.Clear();
            NoIndex.FontWeight = FontWeights.Bold;
            IndexSelector.Items.Add(NoIndex);
            if (m_selectedTable != null)
            {
                foreach (string indexName in m_viewModel.GetIndexes(m_selectedTable))
                {
                    var item = new ComboBoxItem();
                    item.Content = indexName;
                    IndexSelector.Items.Add(item);
                }
            }
            IndexSelector.SelectedItem = NoIndex;
        }

        void TableList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string tableName = TableList.SelectedItem as string;
            m_selectedTable = tableName;
            m_selectedIndex = null;

            SetMode(Mode.Data);

            UpdateIndexList();
            UpdateStatusText(m_selectedTable, m_selectedIndex);
        }

        void UpdateColumnDefinitions(IEnumerable<KeyValuePair<string, Type>> columnNamesAndTypes)
        {
            if (m_selectedTable == null)
            {
                RowGrid.Columns.Clear();
                RowData.DataContext = null;
                return;
            }

            IEnumerable<DBRow> indexInfo;
            if (m_selectedIndex != null)
            {
                indexInfo = m_viewModel.GetIndexInfo(m_selectedTable, m_selectedIndex);
            }
            else
            {
                indexInfo = new List<DBRow>(); // empty
            }

            RowGrid.Columns.Clear();

            foreach (KeyValuePair<string, Type> colspec in columnNamesAndTypes)
            {
                var cellBinding = new Binding();
                cellBinding.Converter = new DBRowValueConverter();
                cellBinding.ConverterParameter = colspec.Key;
                cellBinding.Mode = BindingMode.OneTime;

                FrameworkElementFactory cellFactory;
                if (colspec.Value == typeof(bool?))
                {
                    cellFactory = new FrameworkElementFactory(typeof(CheckBox));
                    cellFactory.SetBinding(CheckBox.IsCheckedProperty, cellBinding);

                    // HACK: Don't allow changes, but don't gray it out either like IsEnabled does.
                    cellFactory.SetValue(CheckBox.IsHitTestVisibleProperty, false);
                    cellFactory.SetValue(CheckBox.FocusableProperty, false);
                }
                else
                {
                    //cellFactory = new FrameworkElementFactory(typeof(ContentControl));
                    //cellFactory.SetBinding(ContentControl.ContentProperty, cellBinding);
                    
                    //cellFactory = new FrameworkElementFactory(typeof(TextBlock));
                    //cellFactory.SetBinding(TextBlock.TextProperty, cellBinding);

                    cellFactory = new FrameworkElementFactory(typeof(TextBox));
                    cellFactory.SetBinding(TextBox.TextProperty, cellBinding);
                    cellFactory.SetValue(TextBox.IsReadOnlyProperty, true);
                    cellFactory.SetValue(TextBox.StyleProperty, FindResource("SelectableTextBlock"));
                }

                var template = new DataTemplate();
                template.VisualTree = cellFactory;

                var gridColumn = new GridViewColumn();
                gridColumn.Header = colspec.Key;
                gridColumn.CellTemplate = template;

                // Bold the column header if it's part of the current index.
                if (indexInfo.Any(o => o.GetValue("ColumnName").Equals(colspec.Key)))
                {
                    var style = new Style(typeof(GridViewColumnHeader));
                    style.Setters.Add(new Setter(GridViewColumnHeader.FontWeightProperty, FontWeights.Bold));
                    gridColumn.HeaderContainerStyle = style;

                    // Set the column cells to bold as well.
                    cellFactory.SetValue(ContentControl.FontWeightProperty, FontWeights.Bold);
                }

                RowGrid.Columns.Add(gridColumn);
            }
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog();
            bool? result = dialog.ShowDialog();

            if (result == true)
            {
                OpenDatabase(dialog.FileName);
            }
        }

        private async void OpenDatabase(string fileName, bool recoveryEnabled = false)
        {
            bool retryWithRecovery = false;

            try
            {
                LoadingScreen.Visibility = Visibility.Visible;

                m_filename = fileName;
                await m_viewModel.OpenDatabaseAsync(fileName, recoveryEnabled);

                TableList.DataContext = m_viewModel.Tables;
                TableList.SelectedIndex = -1;

                Title = "EseView: " + m_filename;
                UpdateStatusText(null, null);

                m_selectedTable = null;
                m_selectedIndex = null;
                IndexInfoToggle.IsChecked = false;

                UpdateIndexList();
                SetMode(Mode.Data);
            }
            catch (Microsoft.Isam.Esent.Interop.EsentDatabaseDirtyShutdownException)
            {
                MessageBoxResult result = MessageBox.Show(
                    "The database was not shut down cleanly. Would you like to recover it?",
                    "Error loading database",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Error);

                if (result == MessageBoxResult.Yes)
                {
                    retryWithRecovery = true;
                }
            }
            catch(Exception ex)
            {
                MessageBox.Show("Error loading database: " + ex.Message, "Error loading database", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                LoadingScreen.Visibility = Visibility.Collapsed;
            }

            if (retryWithRecovery)
            {
                OpenDatabase(fileName, true);
            }
        }

        private void UpdateStatusText(string tableName, string indexName)
        {
            string text = string.Format("{0} tables.", m_viewModel.Tables.Count);
            if (!string.IsNullOrEmpty(tableName))
            {
                text += string.Format(" {0} rows in current table.", m_viewModel.GetRowCount(tableName, indexName));
            }
            StatusText.Text = text;
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                  "EseView by Bill Fraser <wfraser@microsoft.com>\n"
                + "\n"
                + "Note: this is not a Microsoft product;\n"
                + "         this is supported in my own free time.\n"
                + "\n"
                + "https://github.com/wfraser/EseView\n"
                + "\n"
                + "Version " + System.Reflection.Assembly.GetEntryAssembly().GetName().Version
                ,
                "About EseView", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void SetIndex_Click(object sender, RoutedEventArgs e)
        {
            if (m_selectedTable == null)
                return;

            foreach (var item in IndexSelector.Items.OfType<ComboBoxItem>())
            {
                item.FontWeight = FontWeights.Regular;
            }

            var selected = IndexSelector.SelectedItem as ComboBoxItem;
            selected.FontWeight = FontWeights.Bold;

            if (selected == NoIndex)
            {
                m_selectedIndex = null;
            }
            else
            {
                m_selectedIndex = selected.Content as string;
            }

            UpdateStatusText(m_selectedTable, m_selectedIndex);

            if (m_mode == Mode.Data || m_mode == Mode.IndexInfo)
            {
                // Update the display
                SetMode(m_mode);
            }
        }

        private void IndexInfo_Click(object sender, RoutedEventArgs e)
        {
            if (m_selectedTable == null)
            {
                IndexInfoToggle.IsChecked = false;
                return;
            }

            if (ColumnInfoToggle.IsChecked.GetValueOrDefault(false))
            {
                ColumnInfoToggle.IsChecked = false;
            }

            if (IndexInfoToggle.IsChecked.GetValueOrDefault(false))
            {
                SetMode(Mode.IndexInfo);
            }
            else
            {
                SetMode(Mode.Data);
            }
        }

        private void ColumnInfo_Click(object sender, RoutedEventArgs e)
        {
            if (m_selectedTable == null)
            {
                ColumnInfoToggle.IsChecked = false;
                return;
            }

            if (IndexInfoToggle.IsChecked.GetValueOrDefault(false))
            {
                IndexInfoToggle.IsChecked = false;
            }

            if (ColumnInfoToggle.IsChecked.GetValueOrDefault(false))
            {
                SetMode(Mode.ColumnInfo);
            }
            else
            {
                SetMode(Mode.Data);
            }
        }

        void SetMode(Mode mode, bool virtualizing = true)
        {
            m_mode = mode;
            switch (mode)
            {
                case Mode.Data:
                    IndexInfoToggle.IsChecked = false;
                    ColumnInfoToggle.IsChecked = false;
                    UpdateColumnDefinitions(m_viewModel.GetColumnNamesAndTypes(m_selectedTable));
                    if (virtualizing)
                        RowData.DataContext = m_viewModel.VirtualRows(m_selectedTable, m_selectedIndex);
                    else
                        RowData.DataContext = m_viewModel.Rows(m_selectedTable, m_selectedIndex);
                    break;
                case Mode.IndexInfo:
                    IndexInfoToggle.IsChecked = true;
                    ColumnInfoToggle.IsChecked = false;
                    UpdateColumnDefinitions(m_viewModel.GetIndexColumnNamesAndTypes(m_selectedTable, m_selectedIndex));
                    RowData.DataContext = m_viewModel.GetIndexInfo(m_selectedTable, m_selectedIndex);
                    break;
                case Mode.ColumnInfo:
                    IndexInfoToggle.IsChecked = false;
                    ColumnInfoToggle.IsChecked = true;
                    //TODO
                    break;
            }
        }

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            Search();
        }

        private void Search()
        {
            bool caseSensitive = SearchCaseSensitive.IsChecked.GetValueOrDefault(false);
            bool isRegex = SearchRegex.IsChecked.GetValueOrDefault(false);

            string searchTerm = SearchBox.Text;
            if (!isRegex && !caseSensitive)
            {
                searchTerm = searchTerm.ToLower();
            }

            for (int rowIndex = RowData.SelectedIndex + 1; rowIndex < RowData.Items.Count; rowIndex++)
            {
                DBRow row = (DBRow)RowData.Items[rowIndex];

                for (int col = 0; col < row.NumColumns; col++)
                {
                    object value = row[col];
                    if (value == null)
                        continue;

                    string strValue = value.ToString();

                    bool match = false;
                    if (SearchRegex.IsChecked.GetValueOrDefault(false))
                    {
                        match = System.Text.RegularExpressions.Regex.Match(
                            strValue,
                            searchTerm,
                            caseSensitive
                                ? System.Text.RegularExpressions.RegexOptions.None
                                : System.Text.RegularExpressions.RegexOptions.IgnoreCase
                            ).Success;
                    }
                    else
                    {
                        string haystack = strValue;
                        if (!caseSensitive)
                        {
                            haystack = haystack.ToLower();
                        }

                        match = haystack.Contains(searchTerm);
                    }

                    if (match)
                    {
                        ListViewItem viewItem = (ListViewItem)RowData.ItemContainerGenerator.ContainerFromItem(row);
                        if (viewItem == null)
                        {
                            RowData.ScrollIntoView(row);
                            viewItem = (ListViewItem)RowData.ItemContainerGenerator.ContainerFromItem(row);
                        }

                        if (viewItem != null)
                        {
                            RowData.SelectedItem = null;
                            viewItem.IsSelected = true;
                        }

                        return;
                    }
                }
            }

            MessageBox.Show("No match.");
        }

        private void SearchBox_PreviewKeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                e.Handled = true;
                Search();
            }
        }

        private void Dump_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(m_selectedTable))
                return;

            var dialog = new Microsoft.Win32.SaveFileDialog();
            dialog.FileName = "db.xml";
            dialog.DefaultExt = "xml";
            dialog.Filter = "XML (*.xml)|*.xml|All Files (*.*)|*.*";
            dialog.OverwritePrompt = false;
            bool? result = dialog.ShowDialog();

            if (result.HasValue && result.Value)
            {
                using (var output = new System.IO.FileStream(dialog.FileName, System.IO.FileMode.OpenOrCreate))
                {
                    m_viewModel.DumpTable(m_selectedTable, output);
                }
            }
        }
    }
}
