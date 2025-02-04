﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using Xbim.Common;
using Xbim.CobieLiteUk;
using Xbim.CobieLiteUk.Validation;
using Xbim.Ifc;
using Xbim.Presentation;
using Xbim.Presentation.XplorerPluginSystem;
using Xbim.WindowsUI.DPoWValidation.IO;
using Xbim.WindowsUI.DPoWValidation.ViewModels;
using Xbim.WindowsUI.DPoWValidation.Extensions;
using System.IO;
using Microsoft.Extensions.Logging;

namespace XplorerPlugin.DPoW
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>S
    [XplorerUiElement(PluginWindowUiContainerEnum.LayoutAnchorable, PluginWindowActivation.OnMenu, "Digital Plan of Works")]
    public partial class MainWindow : IXbimXplorerPluginWindow 
    {
        private static readonly ILogger Log = XbimLogging.CreateLogger<MainWindow>();

        public MainWindow()
        {
            InitializeComponent();
            IsFileOpen = false;
        }
        // xml navigation sample at http://support.microsoft.com/kb/308333

        private void OpenFile(object sender, RoutedEventArgs e)
        {
            var supportedFiles = new []
            {
                "All supprted files|*.xlsx;*.xls;*.xml;*.json;*.zip",
                "Validation requirement Excel|*.xlsx;*.xls",
                "Validation requirement XML|*.xml",
                "Validation requirement json|*.json",
                "Validation requirement zip|*.zip"
            };

            var openFile = new OpenFileDialog {Filter = string.Join("|", supportedFiles)};
            var res = openFile.ShowDialog();

            if (!res.HasValue || !res.Value) 
                return;
            var r = new FacilityReader();
            ReqFacility = r.LoadFacility(openFile.FileName);
            TestValidation();
        }

        private void SetFacility(Facility facility)
        {
            if (facility == null)
            {
                IsFileOpen = false;
                return;
            }
            ViewFacility = facility;
            // todo: initialise component viewmodel 
            // FacilityViewer.DataContext = new DPoWFacilityViewModel(ReqFacility);

            IsFileOpen = true;
            try
            {
                var clss  = facility.AssetTypes.Where(at => at.Categories != null)
                    .SelectMany(x => x.Categories)
                    .Select(c => c.Code)
                    .Distinct().ToList();
                clss.Add("*");
              
                Classifications.ItemsSource = clss;
            }
            catch (Exception ex)
            {
                Log.LogError(0, ex, "Error setting facility");
            }
        }

        private bool IsFileOpen
        {
            get
            {
                return false;
            }
            set
            {
                // ReSharper disable once RedundantBoolCompare
                if (value == true)
                {
                    Ui.Visibility = Visibility.Visible;
                    OpenButton.Visibility = Visibility.Hidden;
                }
                else
                {
                    Ui.Visibility = Visibility.Hidden;
                    OpenButton.Visibility = Visibility.Visible;
                }
                //PropertyChanged.Invoke(this, new PropertyChangedEventArgs("OpenButtonVisibility"));
                //PropertyChanged.Invoke(this, new PropertyChangedEventArgs("UIVisibility"));
            }
        }

        public Visibility OpenButtonVisibility { get { return (IsFileOpen) ? Visibility.Hidden : Visibility.Visible; } }
        public Visibility UiVisibility { get { return (!IsFileOpen) ? Visibility.Hidden : Visibility.Visible; } }
        
       
        private IXbimXplorerPluginMasterWindow _xpWindow;

        // -----------------------------
        // plugin system related section
        //

        public void BindUi(IXbimXplorerPluginMasterWindow mainWindow)
        {
            _xpWindow = mainWindow;
            SetBinding(SelectedItemProperty, new Binding("SelectedItem") { Source = mainWindow, Mode = BindingMode.OneWay });
            SetBinding(ModelProperty, new Binding()); // whole datacontext binding, see http://stackoverflow.com/questions/8343928/how-can-i-create-a-binding-in-code-behind-that-doesnt-specify-a-path
        }

        // SelectedEntity
        public IPersistEntity SelectedEntity
        {
            get { return (IPersistEntity)GetValue(SelectedItemProperty); }
            set { SetValue(SelectedItemProperty, value); }
        }

        public static DependencyProperty SelectedItemProperty =
            DependencyProperty.Register("SelectedEntity", typeof(IPersistEntity), typeof(MainWindow), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits,
                                                                      OnSelectedEntityChanged));


        private static void OnSelectedEntityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = d as MainWindow;
            if (ctrl == null) 
                return;
            switch (e.Property.Name)
            {
                case "Model":
                    var model = e.NewValue as IfcStore;
                    if (model != null && model.FileName != null)
                    {
                        try
                        {
                            ctrl.ModelFacility = FacilityFromIfcConverter.FacilityFromModel(model);
                        }
                        catch (Exception ex)
                        {
                            Log.LogError(0, ex, "Error in generating Facility from model {filename}",model.FileName);
                            ctrl.ModelFacility = null;
                        }
                    }
                    else
                    {
                        ctrl.ModelFacility = null;
                    }
                    ctrl.TestValidation();
                    break;
                case "SelectedEntity":
                    break;
            }
        }

        private void TestValidation()
        {
            if (ReqFacility == null || ModelFacility == null)
                return;
            var f = new FacilityValidator();
            ValFacility = f.Validate(ReqFacility, ModelFacility);
            PrepareAssetResolve(ValFacility);
            SetFacility(ValFacility);
        }

        private Dictionary<int, Asset> _verifiedItems = new Dictionary<int, Asset>();

        private void PrepareAssetResolve(Facility valFacility)
        {
            _verifiedItems = new Dictionary<int, Asset>();
            if (valFacility.AssetTypes == null)
            {
                Log.LogWarning("No AssetTypes defined in validated facility.");
                return;
            }
            foreach (var valFacilityAssetType in valFacility.AssetTypes)
            {
                if (valFacilityAssetType.Assets == null)
                    continue;
                foreach (var asset in valFacilityAssetType.Assets)
                {
                    int iEl;
                    if (int.TryParse(asset.ExternalId, out iEl))
                    {
                        _verifiedItems.Add(iEl, asset);
                    }
                }
            }
        }

        // Model
        public IModel Model
        {
            get { return (IModel)GetValue(ModelProperty); }
            set { SetValue(ModelProperty, value); }
        }

        public static DependencyProperty ModelProperty =
            DependencyProperty.Register("Model", typeof(IModel), typeof(MainWindow), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits,
                                                                      OnSelectedEntityChanged));

        internal Facility ModelFacility;
        internal Facility ReqFacility;
        internal Facility ValFacility;
        internal Facility ViewFacility;

        public string WindowTitle => "Digital Plan of Work";

        private void TrafficLight(object sender, RoutedEventArgs e)
        {
            var ls = new TrafficLightStyler(Model, this);
            ls.UseAmber = _useAmber;
            _xpWindow.DrawingControl.DefaultLayerStyler = ls;
            _xpWindow.DrawingControl.ReloadModel(DrawingControl3D.ModelRefreshOptions.ViewPreserveAll);
        }

        private void CloseFile(object sender, RoutedEventArgs e)
        {
            ReqFacility = null;
            LstAssets.ItemsSource = null;
            Classifications.ItemsSource = null;
            IsFileOpen = false;
        }

        bool _useAmber = true;

        private void TranspToggle(object sender, MouseButtonEventArgs e)
        {
            UnMatched.Fill = _useAmber 
                ? Brushes.Transparent 
                : Brushes.Orange;

            _useAmber = !_useAmber;
        }

        private void UpdateList(object sender, SelectionChangedEventArgs e)
        {
            // empty if needed
            var lst = new ObservableCollection<AssetViewModel>();
            LstAssets.ItemsSource = lst;

            var selectedCode = Classifications.SelectedItem?.ToString();
            var IgnoreCode = (selectedCode == "*");
            
            if (ViewFacility.AssetTypes == null)
                return;
            foreach (var assetType in ViewFacility.AssetTypes.Where(x => x.Categories != null))
            {
                var valid = IgnoreCode || assetType.Categories.Any(x => x.Code == selectedCode);
                if (!valid)
                    continue;
                if (assetType.Assets == null)
                    continue;
                foreach (var asset in assetType.Assets)
                {
                    lst.Add(new AssetViewModel(asset));
                }               
            }   
        }

        private void GotoAsset(object sender, MouseButtonEventArgs e)
        {
            _xpWindow.DrawingControl.ZoomSelected();
        }

        private void SetSelectedAsset(object sender, SelectionChangedEventArgs e)
        {
            Report.Text = "";
            var avm = LstAssets.SelectedItem as AssetViewModel;
            if (avm == null)
                return;
            var selectedLabel = avm.EntityLabel;
            if (!selectedLabel.HasValue)
                return;
            _xpWindow.SelectedItem = Model.Instances[selectedLabel.Value];

            Report.Text = avm.Description + Environment.NewLine;
            foreach (var item in avm.RequirementResults)
            {
                Report.Text += $"- {item.Name} ({item.Type})" + Environment.NewLine;
            }
        }

        private void ViewModel(object sender, RoutedEventArgs e)
        {
            SetFacility(ModelFacility);
        }

        private void ViewReq(object sender, RoutedEventArgs e)
        {
            SetFacility(ReqFacility);
        }

        private void ViewRes(object sender, RoutedEventArgs e)
        {
            SetFacility(ValFacility);
        }

        public Asset ResolveVerifiedAsset(IPersistEntity ent)
        {
            Asset a;
            return _verifiedItems.TryGetValue(ent.EntityLabel, out a) ? a : null;
        }

        private void Export(object sender, RoutedEventArgs e)
        {
            var m = Model as IfcStore;
            if (m == null)
                return;
            var newName = Path.ChangeExtension(m.FileName, ".report.xlsx");
            var ret = ValFacility.ExportFacility(new FileInfo(newName));
            MessageBox.Show(ret);
        }

        private void OpenUI(object sender, RoutedEventArgs e)
        {
            XbimDPoWTools.MainWindow m = new XbimDPoWTools.MainWindow();
            m.Show();
        }
    }
}
