﻿// The MIT License(MIT)
//
// Copyright(c) 2021 Alberto Rodriguez Orozco & LiveCharts Contributors
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using LiveChartsCore.Drawing;
using LiveChartsCore.Kernel;
using LiveChartsCore.Kernel.Events;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView.Drawing;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;
using Microsoft.Maui.Essentials;
using Microsoft.Maui.Graphics;
using SkiaSharp.Views.Maui;

namespace LiveChartsCore.SkiaSharpView.Maui
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class CartesianChart : ContentView, ICartesianChartView<SkiaSharpDrawingContext>
    {
        #region fields

        private Chart<SkiaSharpDrawingContext>? _core;
        private readonly CollectionDeepObserver<ISeries> _seriesObserver;
        private readonly CollectionDeepObserver<ICartesianAxis> _xObserver;
        private readonly CollectionDeepObserver<ICartesianAxis> _yObserver;
        private readonly CollectionDeepObserver<Section<SkiaSharpDrawingContext>> _sectionsObserverer;
        private Grid? _grid;

        #endregion

        /// <summary>
        /// Initializes a new instance of the <see cref="CartesianChart"/> class.
        /// </summary>
        /// <exception cref="Exception">Default colors are not valid</exception>
        public CartesianChart()
        {
            InitializeComponent();

            if (!LiveCharts.IsConfigured) LiveCharts.Configure(LiveChartsSkiaSharp.DefaultPlatformBuilder);

            var stylesBuilder = LiveCharts.CurrentSettings.GetTheme<SkiaSharpDrawingContext>();
            var initializer = stylesBuilder.GetVisualsInitializer();
            if (stylesBuilder.CurrentColors is null || stylesBuilder.CurrentColors.Length == 0)
                throw new Exception("Default colors are not valid");
            initializer.ApplyStyleToChart(this);

            InitializeCore();
            SizeChanged += OnSizeChanged;

            _seriesObserver = new CollectionDeepObserver<ISeries>(OnDeepCollectionChanged, OnDeepCollectionPropertyChanged, true);
            _xObserver = new CollectionDeepObserver<ICartesianAxis>(OnDeepCollectionChanged, OnDeepCollectionPropertyChanged, true);
            _yObserver = new CollectionDeepObserver<ICartesianAxis>(OnDeepCollectionChanged, OnDeepCollectionPropertyChanged, true);
            _sectionsObserverer = new CollectionDeepObserver<Section<SkiaSharpDrawingContext>>(
                OnDeepCollectionChanged, OnDeepCollectionPropertyChanged, true);

            XAxes = new List<ICartesianAxis>() { LiveCharts.CurrentSettings.AxisProvider() };
            YAxes = new List<ICartesianAxis>() { LiveCharts.CurrentSettings.AxisProvider() };
            Series = new ObservableCollection<ISeries>();

            canvas.SkCanvasView.EnableTouchEvents = true;
            canvas.SkCanvasView.Touch += SkCanvasView_Touch;

            if (_core is null) throw new Exception("Core not found!");
            _core.Measuring += OnCoreMeasuring;
            _core.UpdateStarted += OnCoreUpdateStarted;
            _core.UpdateFinished += OnCoreUpdateFinished;
        }

        #region bindable properties 

        /// <summary>
        /// The sync context property.
        /// </summary>
        public static readonly BindableProperty SyncContextProperty =
            BindableProperty.Create(
                nameof(SyncContext), typeof(object), typeof(CartesianChart), new ObservableCollection<ISeries>(), BindingMode.Default, null,
                (BindableObject o, object oldValue, object newValue) =>
                {
                    var chart = (CartesianChart)o;
                    chart.CoreCanvas.Sync = newValue;
                    if (chart._core is null) return;
                    chart._core.Update();
                });

        /// <summary>
        /// The series property.
        /// </summary>
        public static readonly BindableProperty SeriesProperty =
            BindableProperty.Create(
                nameof(Series), typeof(IEnumerable<ISeries>), typeof(CartesianChart), new ObservableCollection<ISeries>(), BindingMode.Default, null,
                (BindableObject o, object oldValue, object newValue) =>
                {
                    var chart = (CartesianChart)o;
                    var seriesObserver = chart._seriesObserver;
                    seriesObserver.Dispose((IEnumerable<ISeries>)oldValue);
                    seriesObserver.Initialize((IEnumerable<ISeries>)newValue);
                    if (chart._core is null) return;
                    chart._core.Update();
                });

        /// <summary>
        /// The x axes property
        /// </summary>
        public static readonly BindableProperty XAxesProperty =
            BindableProperty.Create(
                nameof(XAxes), typeof(IEnumerable<ICartesianAxis>), typeof(CartesianChart), new List<ICartesianAxis>() { new Axis() },
                BindingMode.Default, null, (BindableObject o, object oldValue, object newValue) =>
                {
                    var chart = (CartesianChart)o;
                    var observer = chart._xObserver;
                    observer.Dispose((IEnumerable<ICartesianAxis>)oldValue);
                    observer.Initialize((IEnumerable<ICartesianAxis>)newValue);
                    if (chart._core is null) return;
                    chart._core.Update();
                });

        /// <summary>
        /// The y axes property.
        /// </summary>
        public static readonly BindableProperty YAxesProperty =
            BindableProperty.Create(
                nameof(YAxes), typeof(IEnumerable<ICartesianAxis>), typeof(CartesianChart), new List<ICartesianAxis>() { new Axis() },
                BindingMode.Default, null, (BindableObject o, object oldValue, object newValue) =>
                {
                    var chart = (CartesianChart)o;
                    var observer = chart._yObserver;
                    observer.Dispose((IEnumerable<ICartesianAxis>)oldValue);
                    observer.Initialize((IEnumerable<ICartesianAxis>)newValue);
                    if (chart._core is null) return;
                    chart._core.Update();
                });

        /// <summary>
        /// The sections property.
        /// </summary>
        public static readonly BindableProperty SectionsProperty =
            BindableProperty.Create(
                nameof(Sections), typeof(IEnumerable<Section<SkiaSharpDrawingContext>>), typeof(CartesianChart), new List<Section<SkiaSharpDrawingContext>>(),
                BindingMode.Default, null, (BindableObject o, object oldValue, object newValue) =>
                {
                    var chart = (CartesianChart)o;
                    var observer = chart._sectionsObserverer;
                    observer.Dispose((IEnumerable<Section<SkiaSharpDrawingContext>>)oldValue);
                    observer.Initialize((IEnumerable<Section<SkiaSharpDrawingContext>>)newValue);
                    if (chart._core is null) return;
                    chart._core.Update();
                });

        /// <summary>
        /// The draw margin frame property.
        /// </summary>
        public static readonly BindableProperty DrawMarginFrameProperty =
            BindableProperty.Create(
                nameof(DrawMarginFrame), typeof(DrawMarginFrame<SkiaSharpDrawingContext>), typeof(CartesianChart), null,
                BindingMode.Default, null, OnBindablePropertyChanged);

        /// <summary>
        /// The draw margin property.
        /// </summary>
        public static readonly BindableProperty DrawMarginProperty =
            BindableProperty.Create(
                nameof(DrawMargin), typeof(Margin), typeof(CartesianChart), null, BindingMode.Default, null, OnBindablePropertyChanged);

        /// <summary>
        /// The zoom mode property.
        /// </summary>
        public static readonly BindableProperty ZoomModeProperty =
            BindableProperty.Create(
                nameof(ZoomMode), typeof(ZoomAndPanMode), typeof(CartesianChart),
                LiveCharts.CurrentSettings.DefaultZoomMode, BindingMode.Default, null);

        /// <summary>
        /// The zooming speed property.
        /// </summary>
        public static readonly BindableProperty ZoomingSpeedProperty =
            BindableProperty.Create(
                nameof(ZoomingSpeed), typeof(double), typeof(CartesianChart),
                LiveCharts.CurrentSettings.DefaultZoomSpeed, BindingMode.Default, null);

        /// <summary>
        /// The animations speed property.
        /// </summary>
        public static readonly BindableProperty AnimationsSpeedProperty =
           BindableProperty.Create(
               nameof(AnimationsSpeed), typeof(TimeSpan), typeof(CartesianChart), LiveCharts.CurrentSettings.DefaultAnimationsSpeed);

        /// <summary>
        /// The easing function property.
        /// </summary>
        public static readonly BindableProperty EasingFunctionProperty =
            BindableProperty.Create(
                nameof(EasingFunction), typeof(Func<float, float>), typeof(CartesianChart),
                LiveCharts.CurrentSettings.DefaultEasingFunction);

        /// <summary>
        /// The legend position property.
        /// </summary>
        public static readonly BindableProperty LegendPositionProperty =
            BindableProperty.Create(
                nameof(LegendPosition), typeof(LegendPosition), typeof(CartesianChart),
                LiveCharts.CurrentSettings.DefaultLegendPosition, propertyChanged: OnBindablePropertyChanged);

        /// <summary>
        /// The legend orientation property.
        /// </summary>
        public static readonly BindableProperty LegendOrientationProperty =
            BindableProperty.Create(
                nameof(LegendOrientation), typeof(LegendOrientation), typeof(CartesianChart),
                LiveCharts.CurrentSettings.DefaultLegendOrientation, propertyChanged: OnBindablePropertyChanged);

        /// <summary>
        /// The legend template property.
        /// </summary>
        public static readonly BindableProperty LegendTemplateProperty =
            BindableProperty.Create(
                nameof(LegendTemplate), typeof(DataTemplate), typeof(CartesianChart), null, propertyChanged: OnBindablePropertyChanged);

        /// <summary>
        /// The legend font family property.
        /// </summary>
        public static readonly BindableProperty LegendFontFamilyProperty =
            BindableProperty.Create(
                nameof(LegendFontFamily), typeof(string), typeof(CartesianChart), null, propertyChanged: OnBindablePropertyChanged);

        /// <summary>
        /// The legend font size property.
        /// </summary>
        public static readonly BindableProperty LegendFontSizeProperty =
            BindableProperty.Create(
                nameof(LegendFontSize), typeof(double), typeof(CartesianChart), 13d, propertyChanged: OnBindablePropertyChanged);

        /// <summary>
        /// The legend text color property.
        /// </summary>
        public static readonly BindableProperty LegendTextBrushProperty =
            BindableProperty.Create(
                nameof(LegendTextBrush), typeof(Color), typeof(CartesianChart),
                new Color(35, 35, 35), propertyChanged: OnBindablePropertyChanged);

        /// <summary>
        /// The legend background property.
        /// </summary>
        public static readonly BindableProperty LegendBackgroundProperty =
            BindableProperty.Create(
                nameof(LegendTextBrush), typeof(Color), typeof(CartesianChart),
                new Color(255, 255, 255), propertyChanged: OnBindablePropertyChanged);

        /// <summary>
        /// The legend font attributes property.
        /// </summary>
        public static readonly BindableProperty LegendFontAttributesProperty =
            BindableProperty.Create(
                nameof(LegendFontAttributes), typeof(FontAttributes), typeof(CartesianChart),
                FontAttributes.None, propertyChanged: OnBindablePropertyChanged);

        /// <summary>
        /// The tool tip position property.
        /// </summary>
        public static readonly BindableProperty TooltipPositionProperty =
           BindableProperty.Create(
               nameof(TooltipPosition), typeof(TooltipPosition), typeof(CartesianChart),
               LiveCharts.CurrentSettings.DefaultTooltipPosition, propertyChanged: OnBindablePropertyChanged);

        /// <summary>
        /// The tool tip finding strategy property.
        /// </summary>
        public static readonly BindableProperty TooltipFindingStrategyProperty =
            BindableProperty.Create(
                nameof(TooltipFindingStrategy), typeof(TooltipFindingStrategy), typeof(CartesianChart),
                LiveCharts.CurrentSettings.DefaultTooltipFindingStrategy);

        /// <summary>
        /// The tool tip template property.
        /// </summary>
        public static readonly BindableProperty TooltipTemplateProperty =
            BindableProperty.Create(
                nameof(TooltipTemplate), typeof(DataTemplate), typeof(CartesianChart), null, propertyChanged: OnBindablePropertyChanged);

        /// <summary>
        /// The tool tip font family property.
        /// </summary>
        public static readonly BindableProperty TooltipFontFamilyProperty =
            BindableProperty.Create(
                nameof(TooltipFontFamily), typeof(string), typeof(CartesianChart), null, propertyChanged: OnBindablePropertyChanged);

        /// <summary>
        /// The tool tip font size property.
        /// </summary>
        public static readonly BindableProperty TooltipFontSizeProperty =
            BindableProperty.Create(
                nameof(TooltipFontSize), typeof(double), typeof(CartesianChart), 13d, propertyChanged: OnBindablePropertyChanged);

        /// <summary>
        /// The tool tip text color property.
        /// </summary>
        public static readonly BindableProperty TooltipTextBrushProperty =
            BindableProperty.Create(
                nameof(TooltipTextBrush), typeof(Color), typeof(CartesianChart),
                new Color(35, 35, 35), propertyChanged: OnBindablePropertyChanged);

        /// <summary>
        /// The tool tip background property.
        /// </summary>
        public static readonly BindableProperty TooltipBackgroundProperty =
            BindableProperty.Create(
                nameof(TooltipBackground), typeof(Color), typeof(CartesianChart),
                new Color(250, 250, 250), propertyChanged: OnBindablePropertyChanged);

        /// <summary>
        /// The tool tip font attributes property
        /// </summary>
        public static readonly BindableProperty TooltipFontAttributesProperty =
            BindableProperty.Create(
                nameof(TooltipFontAttributes), typeof(FontAttributes), typeof(CartesianChart),
                FontAttributes.None, propertyChanged: OnBindablePropertyChanged);

        #endregion

        #region events

        /// <inheritdoc cref="IChartView{TDrawingContext}.Measuring" />
        public event ChartEventHandler<SkiaSharpDrawingContext>? Measuring;

        /// <inheritdoc cref="IChartView{TDrawingContext}.UpdateStarted" />
        public event ChartEventHandler<SkiaSharpDrawingContext>? UpdateStarted;

        /// <inheritdoc cref="IChartView{TDrawingContext}.UpdateFinished" />
        public event ChartEventHandler<SkiaSharpDrawingContext>? UpdateFinished;

        #endregion

        #region properties

        /// <inheritdoc cref="IChartView.DesignerMode" />
        bool IChartView.DesignerMode => DesignMode.IsDesignModeEnabled;

        /// <inheritdoc cref="IChartView.CoreChart" />
        public IChart CoreChart => _core ?? throw new Exception("Core not set yet.");

        LvcColor IChartView.BackColor
        {
            get => Background is not SolidColorBrush b
                ? new LvcColor()
                : LvcColor.FromArgb((byte)b.Color.Alpha, (byte)b.Color.Red, (byte)b.Color.Green, (byte)b.Color.Blue);
            set => Background = new SolidColorBrush(new Color(value.R, value.G, value.B, value.A));
        }

        CartesianChart<SkiaSharpDrawingContext> ICartesianChartView<SkiaSharpDrawingContext>.Core => _core is null ? throw new Exception("core not found") : (CartesianChart<SkiaSharpDrawingContext>)_core;

        LvcSize IChartView.ControlSize => new()
        {
            Width = (float)(canvas.Width * DeviceDisplay.MainDisplayInfo.Density),
            Height = (float)(canvas.Height * DeviceDisplay.MainDisplayInfo.Density)
        };

        /// <inheritdoc cref="IChartView{TDrawingContext}.CoreCanvas" />
        public MotionCanvas<SkiaSharpDrawingContext> CoreCanvas => canvas.CanvasCore;

        //Grid IMobileChart.LayoutGrid => _grid ??= this.FindByName<Grid>("gridLayout");

        //BindableObject IMobileChart.Canvas => canvas;

        //BindableObject IMobileChart.Legend => legend;

        /// <inheritdoc cref="IChartView.SyncContext" />
        public object SyncContext
        {
            get => GetValue(SyncContextProperty);
            set => SetValue(SyncContextProperty, value);
        }

        /// <inheritdoc cref="IChartView.DrawMargin" />
        public Margin? DrawMargin
        {
            get => (Margin?)GetValue(DrawMarginProperty);
            set => SetValue(DrawMarginProperty, value);
        }

        /// <inheritdoc cref="ICartesianChartView{TDrawingContext}.Series" />
        public IEnumerable<ISeries> Series
        {
            get => (IEnumerable<ISeries>)GetValue(SeriesProperty);
            set => SetValue(SeriesProperty, value);
        }

        /// <inheritdoc cref="ICartesianChartView{TDrawingContext}.XAxes" />
        public IEnumerable<ICartesianAxis> XAxes
        {
            get => (IEnumerable<ICartesianAxis>)GetValue(XAxesProperty);
            set => SetValue(XAxesProperty, value);
        }

        /// <inheritdoc cref="ICartesianChartView{TDrawingContext}.YAxes" />
        public IEnumerable<ICartesianAxis> YAxes
        {
            get => (IEnumerable<ICartesianAxis>)GetValue(YAxesProperty);
            set => SetValue(YAxesProperty, value);
        }

        /// <inheritdoc cref="ICartesianChartView{TDrawingContext}.Sections" />
        public IEnumerable<Section<SkiaSharpDrawingContext>> Sections
        {
            get => (IEnumerable<Section<SkiaSharpDrawingContext>>)GetValue(SectionsProperty);
            set => SetValue(SectionsProperty, value);
        }

        /// <inheritdoc cref="ICartesianChartView{TDrawingContext}.DrawMarginFrame" />
        public DrawMarginFrame<SkiaSharpDrawingContext>? DrawMarginFrame
        {
            get => (DrawMarginFrame<SkiaSharpDrawingContext>)GetValue(DrawMarginFrameProperty);
            set => SetValue(DrawMarginFrameProperty, value);
        }

        /// <inheritdoc cref="IChartView.AnimationsSpeed" />
        public TimeSpan AnimationsSpeed
        {
            get => (TimeSpan)GetValue(AnimationsSpeedProperty);
            set => SetValue(AnimationsSpeedProperty, value);
        }

        /// <inheritdoc cref="IChartView.EasingFunction" />
        public Func<float, float>? EasingFunction
        {
            get => (Func<float, float>)GetValue(EasingFunctionProperty);
            set => SetValue(EasingFunctionProperty, value);
        }

        /// <inheritdoc cref="ICartesianChartView{TDrawingContext}.ZoomMode" />
        public ZoomAndPanMode ZoomMode
        {
            get => (ZoomAndPanMode)GetValue(ZoomModeProperty);
            set => SetValue(ZoomModeProperty, value);
        }

        /// <inheritdoc cref="ICartesianChartView{TDrawingContext}.ZoomingSpeed" />
        public double ZoomingSpeed
        {
            get => (double)GetValue(ZoomingSpeedProperty);
            set => SetValue(ZoomingSpeedProperty, value);
        }

        /// <inheritdoc cref="IChartView.LegendPosition" />
        public LegendPosition LegendPosition
        {
            get => (LegendPosition)GetValue(LegendPositionProperty);
            set => SetValue(LegendPositionProperty, value);
        }

        /// <inheritdoc cref="IChartView.LegendOrientation" />
        public LegendOrientation LegendOrientation
        {
            get => (LegendOrientation)GetValue(LegendOrientationProperty);
            set => SetValue(LegendOrientationProperty, value);
        }

        /// <summary>
        /// Gets or sets the legend template.
        /// </summary>
        /// <value>
        /// The legend template.
        /// </value>
        public DataTemplate LegendTemplate
        {
            get => (DataTemplate)GetValue(LegendTemplateProperty);
            set => SetValue(LegendTemplateProperty, value);
        }

        /// <summary>
        /// Gets or sets the default legend font family.
        /// </summary>
        /// <value>
        /// The legend font family.
        /// </value>
        public string LegendFontFamily
        {
            get => (string)GetValue(LegendFontFamilyProperty);
            set => SetValue(LegendFontFamilyProperty, value);
        }

        /// <summary>
        /// Gets or sets the default size of the legend font.
        /// </summary>
        /// <value>
        /// The size of the legend font.
        /// </value>
        public double LegendFontSize
        {
            get => (double)GetValue(LegendFontSizeProperty);
            set => SetValue(LegendFontSizeProperty, value);
        }

        /// <summary>
        /// Gets or sets the default color of the legend text.
        /// </summary>
        /// <value>
        /// The color of the legend text.
        /// </value>
        public Color LegendTextBrush
        {
            get => (Color)GetValue(LegendTextBrushProperty);
            set => SetValue(LegendTextBrushProperty, value);
        }

        /// <summary>
        /// Gets or sets the default color of the legend background.
        /// </summary>
        /// <value>
        /// The color of the legend background.
        /// </value>
        public Color LegendBackground
        {
            get => (Color)GetValue(LegendBackgroundProperty);
            set => SetValue(LegendBackgroundProperty, value);
        }

        /// <summary>
        /// Gets or sets the default legend font attributes.
        /// </summary>
        /// <value>
        /// The legend font attributes.
        /// </value>
        public FontAttributes LegendFontAttributes
        {
            get => (FontAttributes)GetValue(LegendFontAttributesProperty);
            set => SetValue(LegendFontAttributesProperty, value);
        }

        /// <inheritdoc cref="IChartView{TDrawingContext}.Legend" />
        public IChartLegend<SkiaSharpDrawingContext>? Legend => null;//legend;

        /// <inheritdoc cref="IChartView.TooltipPosition" />
        public TooltipPosition TooltipPosition
        {
            get => (TooltipPosition)GetValue(TooltipPositionProperty);
            set => SetValue(TooltipPositionProperty, value);
        }

        /// <inheritdoc cref="ICartesianChartView{TDrawingContext}.TooltipFindingStrategy" />
        public TooltipFindingStrategy TooltipFindingStrategy
        {
            get => (TooltipFindingStrategy)GetValue(TooltipFindingStrategyProperty);
            set => SetValue(TooltipFindingStrategyProperty, value);
        }

        /// <summary>
        /// Gets or sets the tool tip template.
        /// </summary>
        /// <value>
        /// The tool tip template.
        /// </value>
        public DataTemplate TooltipTemplate
        {
            get => (DataTemplate)GetValue(TooltipTemplateProperty);
            set => SetValue(TooltipTemplateProperty, value);
        }

        /// <summary>
        /// Gets or sets the default tool tip font family.
        /// </summary>
        /// <value>
        /// The tool tip font family.
        /// </value>
        public string TooltipFontFamily
        {
            get => (string)GetValue(TooltipFontFamilyProperty);
            set => SetValue(TooltipFontFamilyProperty, value);
        }

        /// <summary>
        /// Gets or sets the default size of the tool tip font.
        /// </summary>
        /// <value>
        /// The size of the tool tip font.
        /// </value>
        public double TooltipFontSize
        {
            get => (double)GetValue(TooltipFontSizeProperty);
            set => SetValue(TooltipFontSizeProperty, value);
        }

        /// <summary>
        /// Gets or sets the default color of the tool tip text.
        /// </summary>
        /// <value>
        /// The color of the tool tip text.
        /// </value>
        public Color TooltipTextBrush
        {
            get => (Color)GetValue(TooltipTextBrushProperty);
            set => SetValue(TooltipTextBrushProperty, value);
        }

        /// <summary>
        /// Gets or sets the default color of the tool tip background.
        /// </summary>
        /// <value>
        /// The color of the tool tip background.
        /// </value>
        public Color TooltipBackground
        {
            get => (Color)GetValue(TooltipBackgroundProperty);
            set => SetValue(TooltipBackgroundProperty, value);
        }

        /// <summary>
        /// Gets or sets the default tool tip font attributes.
        /// </summary>
        /// <value>
        /// The tool tip font attributes.
        /// </value>
        public FontAttributes TooltipFontAttributes
        {
            get => (FontAttributes)GetValue(TooltipFontAttributesProperty);
            set => SetValue(TooltipFontAttributesProperty, value);
        }

        /// <inheritdoc cref="IChartView{TDrawingContext}.Tooltip" />
        public IChartTooltip<SkiaSharpDrawingContext>? Tooltip => null; //tooltip;

        /// <inheritdoc cref="IChartView{TDrawingContext}.PointStates" />
        public PointStatesDictionary<SkiaSharpDrawingContext> PointStates { get; set; } = new();

        /// <inheritdoc cref="IChartView{TDrawingContext}.AutoUpdateEnabled" />
        public bool AutoUpdateEnabled { get; set; } = true;

        /// <inheritdoc cref="IChartView.UpdaterThrottler" />
        public TimeSpan UpdaterThrottler
        {
            get => _core?.UpdaterThrottler ?? throw new Exception("core not set yet.");
            set
            {
                if (_core is null) throw new Exception("core not set yet.");
                _core.UpdaterThrottler = value;
            }
        }

        #endregion

        /// <inheritdoc cref="ICartesianChartView{TDrawingContext}.ScaleUIPoint(LvcPoint, int, int)" />
        public double[] ScaleUIPoint(LvcPoint point, int xAxisIndex = 0, int yAxisIndex = 0)
        {
            if (_core is null) throw new Exception("core not found");
            var cartesianCore = (CartesianChart<SkiaSharpDrawingContext>)_core;
            return cartesianCore.ScaleUIPoint(point, xAxisIndex, yAxisIndex);
        }

        /// <inheritdoc cref="IChartView{TDrawingContext}.ShowTooltip(IEnumerable{TooltipPoint})"/>
        public void ShowTooltip(IEnumerable<TooltipPoint> points)
        {
            //if (tooltip is null || _core is null) return;

            //((IChartTooltip<SkiaSharpDrawingContext>)tooltip).Show(points, _core);
        }

        /// <inheritdoc cref="IChartView{TDrawingContext}.HideTooltip"/>
        public void HideTooltip()
        {
            //if (tooltip is null || _core is null) return;

            //((IChartTooltip<SkiaSharpDrawingContext>)tooltip).Hide();
        }

        /// <inheritdoc cref="IChartView.SetTooltipStyle(LvcColor, LvcColor)"/>
        public void SetTooltipStyle(LvcColor background, LvcColor textColor)
        {
            TooltipBackground = new Color(background.R, background.G, background.B, background.A);
            TooltipTextBrush = new Color(textColor.R, textColor.G, textColor.B, textColor.A);
        }

        void IChartView.InvokeOnUIThread(Action action)
        {
            MainThread.BeginInvokeOnMainThread(action);
        }

        /// <inheritdoc cref="IChartView.SyncAction(Action)"/>
        public void SyncAction(Action action)
        {
            lock (CoreCanvas.Sync)
            {
                action();
            }
        }

        /// <summary>
        /// Initializes the core.
        /// </summary>
        /// <returns></returns>
        protected void InitializeCore()
        {
            _core = new CartesianChart<SkiaSharpDrawingContext>(this, LiveChartsSkiaSharp.DefaultPlatformBuilder, canvas.CanvasCore);
            _core.Update();
        }

        /// <inheritdoc cref="NavigableElement.OnParentSet"/>
        protected override void OnParentSet()
        {
            base.OnParentSet();

            if (Parent == null) _core?.Unload();
            else _core?.Load();
        }

        /// <summary>
        /// Called when a bindable property changed.
        /// </summary>
        /// <param name="o">The o.</param>
        /// <param name="oldValue">The old value.</param>
        /// <param name="newValue">The new value.</param>
        /// <returns></returns>
        protected static void OnBindablePropertyChanged(BindableObject o, object oldValue, object newValue)
        {
            var chart = (CartesianChart)o;
            if (chart._core is null) return;
            chart._core.Update();
        }

        private void OnDeepCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (_core is null || (sender is IStopNPC stop && !stop.IsNotifyingChanges)) return;
            _core.Update();
        }

        private void OnDeepCollectionPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_core is null || (sender is IStopNPC stop && !stop.IsNotifyingChanges)) return;
            _core.Update();
        }

        private void OnSizeChanged(object? sender, EventArgs e)
        {
            if (_core is null) return;
            _core.Update();
        }

        private void PanGestureRecognizer_PanUpdated(object? sender, PanUpdatedEventArgs e)
        {
            if (_core is null) return;
            if (e.StatusType != GestureStatus.Running) return;

            var c = (CartesianChart<SkiaSharpDrawingContext>)_core;
            var delta = new LvcPoint((float)e.TotalX, (float)e.TotalY);
            var args = new PanGestureEventArgs(delta);
            c.InvokePanGestrue(args);
            if (!args.Handled) c.Pan(delta);
        }

        private void PinchGestureRecognizer_PinchUpdated(object? sender, PinchGestureUpdatedEventArgs e)
        {
            if (e.Status != GestureStatus.Running || Math.Abs(e.Scale - 1) < 0.05 || _core is null) return;

            var c = (CartesianChart<SkiaSharpDrawingContext>)_core;
            var p = e.ScaleOrigin;
            var s = c.ControlSize;

            c.Zoom(
                new LvcPoint((float)(p.X * s.Width), (float)(p.Y * s.Height)),
                e.Scale > 1 ? ZoomDirection.ZoomIn : ZoomDirection.ZoomOut);
        }

        private void SkCanvasView_Touch(object? sender, SKTouchEventArgs e)
        {
            if (_core is null) return;
            if (TooltipPosition == TooltipPosition.Hidden) return;
            var location = new LvcPoint(e.Location.X, e.Location.Y);
            _core.InvokePointerDown(location);
            //((IChartTooltip<SkiaSharpDrawingContext>)tooltip).Show(_core.FindPointsNearTo(location), _core);
        }

        private void OnCoreUpdateFinished(IChartView<SkiaSharpDrawingContext> chart)
        {
            UpdateFinished?.Invoke(this);
        }

        private void OnCoreUpdateStarted(IChartView<SkiaSharpDrawingContext> chart)
        {
            UpdateStarted?.Invoke(this);
        }

        private void OnCoreMeasuring(IChartView<SkiaSharpDrawingContext> chart)
        {
            Measuring?.Invoke(this);
        }
    }
}
