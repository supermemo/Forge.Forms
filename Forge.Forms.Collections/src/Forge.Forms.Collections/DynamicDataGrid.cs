﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using FancyGrid;
using FluentCache;
using FluentCache.Simple;
using Forge.Forms.Annotations;
using Forge.Forms.Collections.Annotations;
using Forge.Forms.Collections.Converters;
using Forge.Forms.Collections.Extensions;
using Forge.Forms.Collections.Interfaces;
using Forge.Forms.Collections.Repositories;
using Forge.Forms.DynamicExpressions;
using Forge.Forms.FormBuilding;
using Forge.Forms.FormBuilding.Defaults;
using MaterialDesignThemes.Wpf;
using PropertyChanged;
using Expression = System.Linq.Expressions.Expression;

namespace Forge.Forms.Collections
{
    [TemplatePart(Name = "PART_DataGrid", Type = typeof(DataGrid))]
    public class DynamicDataGrid : Control, INotifyPropertyChanged
    {
        private List<SortDescription> cachedSortDescriptions =
            new List<SortDescription>();

        private bool canMutate;
        private Type itemType;

        public event PropertyChangedEventHandler PropertyChanged;

        public static readonly DependencyProperty CanCreateActionProperty = DependencyProperty.Register(
            nameof(CanCreateAction), typeof(Func<object, CanExecuteRoutedEventArgs, bool>), typeof(DynamicDataGrid),
            new PropertyMetadata(new Func<object, CanExecuteRoutedEventArgs, bool>((o, args) =>
            {
                if (o is DynamicDataGrid d) d.CanExecuteCreateItem(o, args);

                return args.CanExecute;
            })));

        public static readonly DependencyProperty CanRemoveActionProperty = DependencyProperty.Register(
            nameof(CanRemoveAction), typeof(Func<object, CanExecuteRoutedEventArgs, bool>), typeof(DynamicDataGrid),
            new PropertyMetadata(new Func<object, CanExecuteRoutedEventArgs, bool>((o, args) =>
            {
                if (o is DynamicDataGrid d) d.CanExecuteRemoveItem(o, args);

                return args.CanExecute;
            })));

        public static readonly DependencyProperty CanUpdateActionProperty = DependencyProperty.Register(
            nameof(CanUpdateAction), typeof(Func<object, CanExecuteRoutedEventArgs, bool>), typeof(DynamicDataGrid),
            new PropertyMetadata(new Func<object, CanExecuteRoutedEventArgs, bool>((o, args) =>
            {
                if (o is DynamicDataGrid d) d.CanExecuteUpdateItem(o, args);

                return args.CanExecute;
            })));

        public static readonly DependencyProperty CellStyleProperty = DependencyProperty.Register(
            "CellStyle", typeof(Style), typeof(DynamicDataGrid), new PropertyMetadata(default(Style)));

        public static readonly DependencyProperty CreateActionProperty = DependencyProperty.Register(
            nameof(CreateAction), typeof(Action<object, ExecutedRoutedEventArgs>), typeof(DynamicDataGrid),
            new PropertyMetadata(new Action<object, ExecutedRoutedEventArgs>(
                (o, args) =>
                {
                    if (o is DynamicDataGrid d) d.ExecuteCreateItem(o, args);
                })));

        public static readonly DependencyProperty CreateActionTextProperty = DependencyProperty.Register(
            nameof(CreateActionText), typeof(string), typeof(DynamicDataGrid), new PropertyMetadata("Add"));

        public static readonly DependencyProperty DeleteActionTextProperty = DependencyProperty.Register(
            nameof(DeleteActionText), typeof(string), typeof(DynamicDataGrid), new PropertyMetadata("Delete"));

        public static readonly DependencyProperty EditActionProperty = DependencyProperty.Register(
            nameof(EditAction), typeof(Action<object, ExecutedRoutedEventArgs>), typeof(DynamicDataGrid),
            new PropertyMetadata(new Action<object, ExecutedRoutedEventArgs>(
                (o, args) =>
                {
                    if (o is DynamicDataGrid d) d.ExecuteUpdateItem(o, args);
                })));

        public static readonly DependencyProperty EditActionTextProperty = DependencyProperty.Register(
            nameof(EditActionText), typeof(string), typeof(DynamicDataGrid), new PropertyMetadata("Edit"));

        public static readonly DependencyProperty RemoveActionProperty = DependencyProperty.Register(
            nameof(RemoveAction), typeof(Action<object, ExecutedRoutedEventArgs>), typeof(DynamicDataGrid),
            new FrameworkPropertyMetadata(new Action<object, ExecutedRoutedEventArgs>(
                (o, args) =>
                {
                    if (o is DynamicDataGrid d) d.ExecuteRemoveItem(o, args);
                })));

        public static readonly DependencyProperty RowStyleProperty = DependencyProperty.Register(
            "RowStyle", typeof(Style), typeof(DynamicDataGrid), new PropertyMetadata(default(Style)));

        public static readonly DependencyProperty UseColumnCacheingProperty = DependencyProperty.Register(
            nameof(UseColumnCacheing), typeof(bool), typeof(DynamicDataGrid), new PropertyMetadata(default(bool),
                (o, args) =>
                {
                    if (o is DynamicDataGrid d) d.ReloadColumns();
                }));

        public static readonly DependencyProperty ColumnsProperty = DependencyProperty.Register(
            "Columns", typeof(ObservableCollection<DataGridColumn>), typeof(DynamicDataGrid),
            new PropertyMetadata(new ObservableCollection<DataGridColumn>(), OnColumnsPropertyChanged));

        public static readonly DependencyProperty AutoGenerateColumnsProperty = DependencyProperty.Register(
            "AutoGenerateColumns", typeof(bool), typeof(DynamicDataGrid),
            new PropertyMetadata(true, OnAutoGenerateColumnsChanged));

        /// <summary>
        ///     Identifies the CanUserAdd dependency property.
        /// </summary>
        public static DependencyProperty CanUserAddProperty =
            DependencyProperty.Register("CanUserAdd", typeof(bool), typeof(DynamicDataGrid),
                new PropertyMetadata(true));

        /// <summary>
        ///     Identifies the CanUserEdit dependency property.
        /// </summary>
        public static DependencyProperty CanUserEditProperty =
            DependencyProperty.Register("CanUserEdit", typeof(bool), typeof(DynamicDataGrid),
                new PropertyMetadata(true));

        /// <summary>
        ///     Identifies the CanUserRemove dependency property.
        /// </summary>
        public static DependencyProperty CanUserRemoveProperty =
            DependencyProperty.Register("CanUserRemove", typeof(bool), typeof(DynamicDataGrid),
                new PropertyMetadata(true));

        /// <summary>
        ///     The create dialog negative content property
        /// </summary>
        public static readonly DependencyProperty CreateDialogNegativeContentProperty = DependencyProperty.Register(
            nameof(CreateDialogNegativeContent),
            typeof(string),
            typeof(DynamicDataGrid),
            new FrameworkPropertyMetadata("CANCEL"));

        /// <summary>
        ///     The create dialog negative icon property
        /// </summary>
        public static readonly DependencyProperty CreateDialogNegativeIconProperty =
            DependencyProperty.Register(
                nameof(CreateDialogNegativeIcon),
                typeof(PackIconKind?),
                typeof(DynamicDataGrid),
                new FrameworkPropertyMetadata(PackIconKind.Close));

        /// <summary>
        ///     The create dialog positive content property
        /// </summary>
        public static readonly DependencyProperty CreateDialogPositiveContentProperty =
            DependencyProperty.Register(
                nameof(CreateDialogPositiveContent),
                typeof(string),
                typeof(DynamicDataGrid),
                new FrameworkPropertyMetadata("ADD"));

        /// <summary>
        ///     The create dialog positive icon property
        /// </summary>
        public static readonly DependencyProperty CreateDialogPositiveIconProperty =
            DependencyProperty.Register(
                nameof(CreateDialogPositiveIcon),
                typeof(PackIconKind?),
                typeof(DynamicDataGrid),
                new FrameworkPropertyMetadata(PackIconKind.Check));

        /// <summary>
        ///     Identifies the HeaderStyle dependency property.
        /// </summary>
        public static DependencyProperty HeaderStyleProperty =
            DependencyProperty.Register("HeaderStyle", typeof(DynamicDataGridHeaderStyle), typeof(DynamicDataGrid),
                new PropertyMetadata());

        /// <summary>
        ///     The is filtering enabled property
        /// </summary>
        public static readonly DependencyProperty IsFilteringEnabledProperty =
            DependencyProperty.Register("IsFilteringEnabled", typeof(bool), typeof(DynamicDataGrid),
                new PropertyMetadata(false));

        /// <summary>
        ///     The remove dialog negative content property
        /// </summary>
        public static readonly DependencyProperty RemoveDialogNegativeContentProperty = DependencyProperty.Register(
            nameof(RemoveDialogNegativeContent),
            typeof(string),
            typeof(DynamicDataGrid),
            new FrameworkPropertyMetadata("CANCEL"));

        /// <summary>
        ///     The remove dialog negative icon property
        /// </summary>
        public static readonly DependencyProperty RemoveDialogNegativeIconProperty =
            DependencyProperty.Register(
                nameof(RemoveDialogNegativeIcon),
                typeof(PackIconKind?),
                typeof(DynamicDataGrid),
                new FrameworkPropertyMetadata(PackIconKind.Close));

        /// <summary>
        ///     The remove dialog positive content property
        /// </summary>
        public static readonly DependencyProperty RemoveDialogPositiveContentProperty = DependencyProperty.Register(
            nameof(RemoveDialogPositiveContent),
            typeof(string),
            typeof(DynamicDataGrid),
            new FrameworkPropertyMetadata("REMOVE"));

        /// <summary>
        ///     The remove dialog positive icon property
        /// </summary>
        public static readonly DependencyProperty RemoveDialogPositiveIconProperty =
            DependencyProperty.Register(
                nameof(RemoveDialogPositiveIcon),
                typeof(PackIconKind?),
                typeof(DynamicDataGrid),
                new FrameworkPropertyMetadata(PackIconKind.Delete));

        /// <summary>
        ///     The remove dialog text content property
        /// </summary>
        public static readonly DependencyProperty RemoveDialogTextContentProperty = DependencyProperty.Register(
            nameof(RemoveDialogTextContent),
            typeof(string),
            typeof(DynamicDataGrid),
            new FrameworkPropertyMetadata("Remove item(s)?"));

        /// <summary>
        ///     The remove dialog title content property
        /// </summary>
        public static readonly DependencyProperty RemoveDialogTitleContentProperty = DependencyProperty.Register(
            nameof(RemoveDialogTitleContent),
            typeof(string),
            typeof(DynamicDataGrid),
            new FrameworkPropertyMetadata());

        /// <summary>
        ///     The toggle filter command property
        /// </summary>
        public static readonly DependencyProperty ToggleFilterCommandProperty =
            DependencyProperty.Register("ToggleFilterCommand", typeof(ICommand), typeof(DynamicDataGrid),
                new PropertyMetadata());

        /// <summary>
        ///     The update dialog negative content property
        /// </summary>
        public static readonly DependencyProperty UpdateDialogNegativeContentProperty = DependencyProperty.Register(
            nameof(UpdateDialogNegativeContent),
            typeof(string),
            typeof(DynamicDataGrid),
            new FrameworkPropertyMetadata("CANCEL"));

        /// <summary>
        ///     The update dialog negative icon property
        /// </summary>
        public static readonly DependencyProperty UpdateDialogNegativeIconProperty =
            DependencyProperty.Register(
                nameof(UpdateDialogNegativeIcon),
                typeof(PackIconKind?),
                typeof(DynamicDataGrid),
                new FrameworkPropertyMetadata(PackIconKind.Close));

        /// <summary>
        ///     The update dialog positive content property
        /// </summary>
        public static readonly DependencyProperty UpdateDialogPositiveContentProperty = DependencyProperty.Register(
            nameof(UpdateDialogPositiveContent),
            typeof(string),
            typeof(DynamicDataGrid),
            new FrameworkPropertyMetadata("OK"));

        /// <summary>
        ///     The update dialog positive icon property
        /// </summary>
        public static readonly DependencyProperty UpdateDialogPositiveIconProperty =
            DependencyProperty.Register(
                nameof(UpdateDialogPositiveIcon),
                typeof(PackIconKind?),
                typeof(DynamicDataGrid),
                new FrameworkPropertyMetadata(PackIconKind.Check));

        /// <summary>
        ///     The items source property
        /// </summary>
        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(
                nameof(ItemsSource),
                typeof(IEnumerable),
                typeof(DynamicDataGrid), new PropertyMetadata(null, ItemsSourceChanged));

        /// <summary>
        ///     Identifies the ExcludeItemsMessage dependency property.
        /// </summary>
        public static DependencyProperty ExcludeItemsMessageProperty =
            DependencyProperty.Register("ExcludeItemsMessage", typeof(string), typeof(FilteringDataGrid),
                new PropertyMetadata("Exclude items like this"));

        /// <summary>
        ///     Identifies the IncludeItemsMessage dependency property.
        /// </summary>
        public static DependencyProperty IncludeItemsMessageProperty =
            DependencyProperty.Register("IncludeItemsMessage", typeof(string), typeof(FilteringDataGrid),
                new PropertyMetadata("Include items like this"));

        /// <summary>
        ///     Identifies the MovePrevious dependency property.
        /// </summary>
        public static DependencyProperty MoveBackCommandProperty =
            DependencyProperty.Register("MoveBackCommand", typeof(ICommand), typeof(DynamicDataGrid),
                new PropertyMetadata());

        /// <summary>
        ///     Identifies the FirstPage dependency property.
        /// </summary>
        public static DependencyProperty MoveFirstCommandProperty =
            DependencyProperty.Register("MoveFirstCommand", typeof(ICommand), typeof(DynamicDataGrid),
                new PropertyMetadata());

        /// <summary>
        ///     Identifies the LastPage dependency property.
        /// </summary>
        public static DependencyProperty MoveLastCommandProperty =
            DependencyProperty.Register("MoveLastCommand", typeof(ICommand), typeof(DynamicDataGrid),
                new PropertyMetadata());

        /// <summary>
        ///     Identifies the NextPage dependency property.
        /// </summary>
        public static DependencyProperty MoveNextCommandProperty =
            DependencyProperty.Register("MoveNextCommand", typeof(ICommand), typeof(DynamicDataGrid),
                new PropertyMetadata());

        /// <summary>
        ///     Identifies the NextPage dependency property.
        /// </summary>
        public static DependencyProperty MoveToPageCommandProperty =
            DependencyProperty.Register("MoveToPageCommand", typeof(ICommand), typeof(DynamicDataGrid),
                new PropertyMetadata());

        /// <summary>
        ///     The dialog options property
        /// </summary>
        public static readonly DependencyProperty DialogOptionsProperty =
            DependencyProperty.Register(
                nameof(DialogOptions),
                typeof(DialogOptions),
                typeof(DynamicDataGrid),
                new FrameworkPropertyMetadata(DialogOptions.Default, ItemsSourceChanged));

        /// <summary>
        ///     The add interceptor chain
        /// </summary>
        public static readonly List<ICreateActionInterceptor>
            AddInterceptorChain = new List<ICreateActionInterceptor>();

        public static readonly RoutedCommand CreateItemCommand = new RoutedCommand();

        /// <summary>
        ///     The remove interceptor chain
        /// </summary>
        public static readonly List<IRemoveActionInterceptor> RemoveInterceptorChain =
            new List<IRemoveActionInterceptor>();

        public static readonly RoutedCommand RemoveItemCommand = new RoutedCommand();

        /// <summary>
        ///     The rows per page text property
        /// </summary>
        public static readonly DependencyProperty RowsPerPageTextProperty =
            DependencyProperty.Register("RowsPerPageText", typeof(string), typeof(DynamicDataGrid),
                new PropertyMetadata("Rows per page"));

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register("Title", typeof(string), typeof(DynamicDataGrid),
                new PropertyMetadata(""));

        /// <summary>
        ///     The update interceptor chain
        /// </summary>
        public static readonly List<IUpdateActionInterceptor> UpdateInterceptorChain =
            new List<IUpdateActionInterceptor>();

        public static readonly RoutedCommand UpdateItemCommand = new RoutedCommand();

        /// <summary>
        ///     Identifies the CurrentPage dependency property.
        /// </summary>
        public static DependencyProperty CurrentPageProperty =
            DependencyProperty.Register("CurrentPage", typeof(int), typeof(DynamicDataGrid), new PropertyMetadata(1,
                OnCurrentPageChanged));

        /// <summary>
        ///     The is delete button visible property
        /// </summary>
        public static readonly DependencyProperty IsDeleteButtonVisibleProperty =
            DependencyProperty.Register("IsDeleteButtonVisible", typeof(bool), typeof(DynamicDataGrid),
                new PropertyMetadata(false));

        /// <summary>
        ///     The is filter button visible property
        /// </summary>
        public static readonly DependencyProperty IsFilterButtonVisibleProperty =
            DependencyProperty.Register("IsFilterButtonVisible", typeof(bool), typeof(DynamicDataGrid),
                new PropertyMetadata(true));

        public static readonly DependencyProperty HasCheckboxesColumnProperty = DependencyProperty.Register(
            "HasCheckboxesColumn", typeof(bool), typeof(DynamicDataGrid),
            new PropertyMetadata(default(bool), HasCheckboxColumnChanged));

        public bool AutoGenerateColumns
        {
            get => (bool) GetValue(AutoGenerateColumnsProperty);
            set => SetValue(AutoGenerateColumnsProperty, value);
        }

        public Func<object, CanExecuteRoutedEventArgs, bool> CanCreateAction
        {
            get => (Func<object, CanExecuteRoutedEventArgs, bool>) GetValue(CanCreateActionProperty);
            set => SetValue(CanCreateActionProperty, value);
        }

        public Func<object, CanExecuteRoutedEventArgs, bool> CanRemoveAction
        {
            get => (Func<object, CanExecuteRoutedEventArgs, bool>) GetValue(CanRemoveActionProperty);
            set => SetValue(CanRemoveActionProperty, value);
        }

        public Func<object, CanExecuteRoutedEventArgs, bool> CanUpdateAction
        {
            get => (Func<object, CanExecuteRoutedEventArgs, bool>) GetValue(CanUpdateActionProperty);
            set => SetValue(CanUpdateActionProperty, value);
        }

        /// <summary>
        ///     Gets or sets a value indicating whether this the user can add new items.
        /// </summary>
        /// <value>
        ///     <c>true</c> if this instance can add new items; otherwise, <c>false</c>.
        /// </value>
        public bool CanUserAdd
        {
            get => (bool) GetValue(CanUserAddProperty);
            set => SetValue(CanUserAddProperty, value);
        }

        /// <summary>
        ///     Gets or sets a value indicating whether this instance can add edit items.
        /// </summary>
        /// <value>
        ///     <c>true</c> if this instance can add edit items; otherwise, <c>false</c>.
        /// </value>
        public bool CanUserEdit
        {
            get => (bool) GetValue(CanUserEditProperty);
            set => SetValue(CanUserEditProperty, value);
        }

        /// <summary>
        ///     Gets or sets a value indicating whether this instance can remove items.
        /// </summary>
        /// <value>
        ///     <c>true</c> if this instance can remove items; otherwise, <c>false</c>.
        /// </value>
        public bool CanUserRemove
        {
            get => (bool) GetValue(CanUserRemoveProperty);
            set => SetValue(CanUserRemoveProperty, value);
        }

        public Style CellStyle
        {
            get => (Style) GetValue(CellStyleProperty);
            set => SetValue(CellStyleProperty, value);
        }

        public ObservableCollection<DataGridColumn> Columns
        {
            get => (ObservableCollection<DataGridColumn>) GetValue(ColumnsProperty);
            set => SetValue(ColumnsProperty, value);
        }

        public Action<object, ExecutedRoutedEventArgs> CreateAction
        {
            get => (Action<object, ExecutedRoutedEventArgs>) GetValue(CreateActionProperty);
            set => SetValue(CreateActionProperty, value);
        }

        public string CreateActionText
        {
            get => (string) GetValue(CreateActionTextProperty);
            set => SetValue(CreateActionTextProperty, value);
        }

        public int CurrentMaxItem => Math.Min(TotalItems, CurrentPage * ItemsPerPage);

        public int CurrentMinItem => Math.Min(TotalItems, CurrentMaxItem - ItemsOnPage + 1);

        /// <summary>
        ///     Gets the current page.
        /// </summary>
        /// <value>
        ///     The current page.
        /// </value>
        [AlsoNotifyFor(nameof(PaginationPageNumbers))]
        public int CurrentPage
        {
            get => (int) GetValue(CurrentPageProperty);
            private set
            {
                IsSelectAll = false;
                SetValue(CurrentPageProperty, value);
            }
        }

        public string DeleteActionText
        {
            get => (string) GetValue(DeleteActionTextProperty);
            set => SetValue(DeleteActionTextProperty, value);
        }

        public Action<object, ExecutedRoutedEventArgs> EditAction
        {
            get => (Action<object, ExecutedRoutedEventArgs>) GetValue(EditActionProperty);
            set => SetValue(EditActionProperty, value);
        }

        public string EditActionText
        {
            get => (string) GetValue(EditActionTextProperty);
            set => SetValue(EditActionTextProperty, value);
        }

        /// <summary>
        ///     Gets or sets the exclude items message.
        /// </summary>
        /// <value>
        ///     The exclude items message.
        /// </value>
        public string ExcludeItemsMessage
        {
            get => (string) GetValue(ExcludeItemsMessageProperty);
            set => SetValue(ExcludeItemsMessageProperty, value);
        }

        public bool HasCheckboxesColumn
        {
            get => (bool) GetValue(HasCheckboxesColumnProperty);
            set => SetValue(HasCheckboxesColumnProperty, value);
        }

        /// <summary>
        ///     Gets or sets the header style.
        /// </summary>
        /// <value>
        ///     The header style.
        /// </value>
        public DynamicDataGridHeaderStyle HeaderStyle
        {
            get => (DynamicDataGridHeaderStyle) GetValue(HeaderStyleProperty);
            set => SetValue(HeaderStyleProperty, value);
        }

        /// <summary>
        ///     Gets or sets the include items message.
        /// </summary>
        /// <value>
        ///     The include items message.
        /// </value>
        public string IncludeItemsMessage
        {
            get => (string) GetValue(IncludeItemsMessageProperty);
            set => SetValue(IncludeItemsMessageProperty, value);
        }

        /// <summary>
        ///     Gets a value indicating whether this instance's delete button visible.
        /// </summary>
        /// <value>
        ///     <c>true</c> if this instance has delete button visible; otherwise, <c>false</c>.
        /// </value>
        public bool IsDeleteButtonVisible
        {
            get => (bool) GetValue(IsDeleteButtonVisibleProperty);
            private set => SetValue(IsDeleteButtonVisibleProperty, value);
        }

        /// <summary>
        ///     Gets a value indicating whether this instance's filter button visible.
        /// </summary>
        /// <value>
        ///     <c>true</c> if this instance has filter button visible; otherwise, <c>false</c>.
        /// </value>
        public bool IsFilterButtonVisible
        {
            get => (bool) GetValue(IsFilterButtonVisibleProperty);
            private set => SetValue(IsFilterButtonVisibleProperty, value);
        }

        /// <summary>
        ///     Gets or sets a value indicating whether this instance has filtering enabled.
        /// </summary>
        /// <value>
        ///     <c>true</c> if this instance has filtering enabled; otherwise, <c>false</c>.
        /// </value>
        public bool IsFilteringEnabled
        {
            get => (bool) GetValue(IsFilteringEnabledProperty);
            set => SetValue(IsFilteringEnabledProperty, value);
        }

        /// <summary>
        ///     Gets the items on page.
        /// </summary>
        /// <value>
        ///     The items on page.
        /// </value>
        public int ItemsOnPage => Math.Min(ItemsPerPage, TotalItems - (CurrentPage - 1) * ItemsPerPage);

        /// <summary>
        ///     Gets or sets the items per page.
        /// </summary>
        /// <value>
        ///     The items per page.
        /// </value>
        [AlsoNotifyFor(nameof(LastPage))]
        public int ItemsPerPage { get; set; } = 15;

        /// <summary>
        ///     Gets the last page.
        /// </summary>
        /// <value>
        ///     The last page.
        /// </value>
        public int LastPage => Math.Max((int) Math.Ceiling((double) TotalItems / ItemsPerPage), 1);

        /// <summary>
        ///     Gets or sets the move back command.
        /// </summary>
        /// <value>
        ///     The move back command.
        /// </value>
        public ICommand MoveBackCommand
        {
            get => (ICommand) GetValue(MoveBackCommandProperty);
            set => SetValue(MoveBackCommandProperty, value);
        }

        /// <summary>
        ///     Gets or sets the move first command.
        /// </summary>
        /// <value>
        ///     The move first command.
        /// </value>
        public ICommand MoveFirstCommand
        {
            get => (ICommand) GetValue(MoveFirstCommandProperty);
            set => SetValue(MoveFirstCommandProperty, value);
        }

        /// <summary>
        ///     Gets or sets the move last command.
        /// </summary>
        /// <value>
        ///     The move last command.
        /// </value>
        public ICommand MoveLastCommand
        {
            get => (ICommand) GetValue(MoveLastCommandProperty);
            set => SetValue(MoveLastCommandProperty, value);
        }

        /// <summary>
        ///     Gets or sets the move next command.
        /// </summary>
        /// <value>
        ///     The move next command.
        /// </value>
        public ICommand MoveNextCommand
        {
            get => (ICommand) GetValue(MoveNextCommandProperty);
            set => SetValue(MoveNextCommandProperty, value);
        }

        /// <summary>
        ///     Gets or sets the move to page command.
        /// </summary>
        /// <value>
        ///     The move to page command.
        /// </value>
        public ICommand MoveToPageCommand
        {
            get => (ICommand) GetValue(MoveToPageCommandProperty);
            set => SetValue(MoveToPageCommandProperty, value);
        }

        public Action<object, ExecutedRoutedEventArgs> RemoveAction
        {
            get => (Action<object, ExecutedRoutedEventArgs>) GetValue(RemoveActionProperty);
            set => SetValue(RemoveActionProperty, value);
        }

        /// <summary>
        ///     Gets or sets the rows per page text.
        /// </summary>
        /// <value>
        ///     The rows per page text.
        /// </value>
        public string RowsPerPageText
        {
            get => (string) GetValue(RowsPerPageTextProperty);
            set => SetValue(RowsPerPageTextProperty, value);
        }

        public Style RowStyle
        {
            get => (Style) GetValue(RowStyleProperty);
            set => SetValue(RowStyleProperty, value);
        }

        /// <summary>
        ///     Gets the selected items.
        /// </summary>
        /// <value>
        ///     The selected items.
        /// </value>
        public IList<object> SelectedItems
        {
            get { return CheckedConverter.GetItems(this).Where(i => i.Value).Select(i => i.Key).ToList(); }
        }

        /// <summary>
        ///     Gets or sets the title.
        /// </summary>
        /// <value>
        ///     The title.
        /// </value>
        public string Title
        {
            get => (string) GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        /// <summary>
        ///     Gets or sets the toggle filter command.
        /// </summary>
        /// <value>
        ///     The toggle filter command.
        /// </value>
        public ICommand ToggleFilterCommand
        {
            get => (ICommand) GetValue(ToggleFilterCommandProperty);
            set => SetValue(ToggleFilterCommandProperty, value);
        }

        /// <summary>
        ///     Gets the total items.
        /// </summary>
        /// <value>
        ///     The total items.
        /// </value>
        public int TotalItems => GetIEnumerableCount(ItemsSource) ?? 0;

        public bool UseColumnCacheing
        {
            get => (bool) GetValue(UseColumnCacheingProperty);
            set => SetValue(UseColumnCacheingProperty, value);
        }

        internal List<IColumnCreationInterceptor> ColumnCreationInterceptors { get; } =
            new List<IColumnCreationInterceptor>
            {
                new DefaultColumnCreationInterceptor()
            };

        internal FilteringDataGrid DataGrid { get; set; }

        internal Type ItemType
        {
            get => itemType;
            set
            {
                if (itemType == value) return;

                itemType = value;

                ReloadColumns();
            }
        }

        private static Cache<ColumnRepository> ColumnCache { get; }

        private IEnumerable<object> DatagridSelectedItems
        {
            get
            {
                return DataGrid.GetRows()
                    .Where(row => CheckedConverter.IsChecked(this, row.Item))
                    .Select(row => row.Item).ToList();
            }
        }

        private CheckBox HeaderButton { get; } = new CheckBox
        {
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Top
        };

        private bool IsSelectAll { get; set; }

        private ComboBox PerPageComboBox { get; set; }

        private List<DataGridColumn> ProtectedColumns { get; set; }

        private int SelectedItemsCount => DatagridSelectedItems.Count();

        static DynamicDataGrid()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(DynamicDataGrid),
                new FrameworkPropertyMetadata(typeof(DynamicDataGrid)));

            ColumnCache = new FluentDictionaryCache().WithSource(new ColumnRepository());
        }

        public DynamicDataGrid()
        {
            CommandBindings.Add(new CommandBinding(CreateItemCommand,
                (sender, args) => CreateAction.Invoke(sender, args),
                (sender, args) => args.CanExecute = CanCreateAction.Invoke(sender, args)));
            CommandBindings.Add(new CommandBinding(UpdateItemCommand,
                (sender, args) => EditAction.Invoke(sender, args),
                (sender, args) => args.CanExecute = CanUpdateAction.Invoke(sender, args)));
            CommandBindings.Add(new CommandBinding(RemoveItemCommand,
                (sender, args) => RemoveAction.Invoke(sender, args),
                (sender, args) => args.CanExecute = CanRemoveAction.Invoke(sender, args)));

            MoveNextCommand = new RelayCommand(x => CurrentPage++, o => CurrentPage < LastPage);
            MoveBackCommand = new RelayCommand(x => CurrentPage--, o => CurrentPage > 1);
            MoveLastCommand = new RelayCommand(x => CurrentPage = LastPage, o => CurrentPage < LastPage);
            MoveFirstCommand = new RelayCommand(x => CurrentPage = 1, o => CurrentPage > 1);
            MoveToPageCommand = new RelayCommand(
                x => CurrentPage = int.Parse((string) x),
                o => int.TryParse((string) o, out var val) && val != CurrentPage);
            ToggleFilterCommand = new RelayCommand(x => IsFilteringEnabled = !IsFilteringEnabled);
            CheckboxColumnCommand = new RelayCommand(sender =>
            {
                if (sender is DataGridRow row)
                {
                    CheckedConverter.SetChecked(this, row.Item, !CheckedConverter.IsChecked(this, row.Item));
                    BindingOperations
                        .GetMultiBindingExpression(row.TryFindChild<CheckBox>(), ToggleButton.IsCheckedProperty)
                        ?.UpdateTarget();
                }
            });

            InitializeCellStyleAndColumns();

            PropertyChanged += OnPropertyChanged;
            Loaded += (s, e) => OnItemsSource(ItemsSource);
        }

        private static void OnColumnsPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DynamicDataGrid dynamicDataGrid)
            {
                dynamicDataGrid.ReloadColumns();

                dynamicDataGrid.Columns.CollectionChanged += (sender, args) => { dynamicDataGrid.ReloadColumns(); };
            }
        }

        private static void OnAutoGenerateColumnsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DynamicDataGrid dynamicDataGrid) dynamicDataGrid.ReloadColumns();
        }

        public void InitializeCellStyleAndColumns()
        {
            if (CellStyle == null)
                CellStyle = TryFindResource("CustomDataGridCell") as Style;

            Columns.CollectionChanged += (sender, args) => { ReloadColumns(); };
        }

        private static void HasCheckboxColumnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DynamicDataGrid dynamicDataGrid)
                dynamicDataGrid.ReloadColumns();
        }

        public void ReloadColumns()
        {
            if (DataGrid == null || itemType == null) return;

            if (Columns != null && Columns.Any())
                foreach (var dataGridColumn in Columns)
                {
                    if (DataGrid.Columns.Any(i => Equals(i.Header, dataGridColumn.Header)))
                        continue;

                    DataGrid.Columns.Insert(0, dataGridColumn);
                }

            if (!AutoGenerateColumns)
            {
                CreateCheckboxColumn();
                return;
            }

            foreach (var propertyInfo in ItemType
                .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(i => i.GetCustomAttribute<FieldIgnoreAttribute>() == null &&
                            i.GetCustomAttribute<CrudIgnoreAttribute>() == null)
                .Reverse())
            {
                DataGridColumn column = null;

                if (UseColumnCacheing)
                    column = ColumnCache.Method(r => r.GetColumn(propertyInfo, this))
                        .GetValue();
                else
                    column = ColumnRepository.GetColumnStatic(propertyInfo, this);

                if (column != null && !DataGrid.Columns.Contains(column) &&
                    DataGrid.Columns.All(i => !Equals(i.Header, column.Header)))
                    DataGrid.Columns.Insert(0, column);
            }

            CreateCheckboxColumn();
        }

        private void OnPropertyChanged(object sender1, PropertyChangedEventArgs propertyChangedEventArgs)
        {
            if (propertyChangedEventArgs.PropertyName == nameof(SelectedItems))
            {
                IsDeleteButtonVisible = SelectedItems.Any() && HeaderStyle != DynamicDataGridHeaderStyle.Alternative;
                IsFilterButtonVisible = !SelectedItems.Any();
            }
        }

        internal void UpdateHeaderButton()
        {
            var items = DatagridSelectedItems.ToList();
            if (items.Count > 0 && items.Count < DataGrid.Items.Count)
                HeaderButton.IsChecked = null;
            else if (items.Count == DataGrid.Items.Count)
                HeaderButton.IsChecked = true;
            else if (items.Count == 0) HeaderButton.IsChecked = false;
        }

        private static void OnCurrentPageChanged(DependencyObject x, DependencyPropertyChangedEventArgs y)
        {
            if (x is DynamicDataGrid grid) grid.DataGridOnSorting(null, null);
        }

        private void CreateCheckboxColumn()
        {
            var dataGridColumn = DataGrid.Columns.FirstOrDefault(i => Equals(i.Header, HeaderButton));

            if (!HasCheckboxesColumn || dataGridColumn != null)
            {
                if (!HasCheckboxesColumn && dataGridColumn != null) DataGrid.Columns.Remove(dataGridColumn);

                return;
            }

            var rowCheckBox = new FrameworkElementFactory(typeof(CheckBox));
            rowCheckBox.SetValue(MaxWidthProperty, 18.0);
            rowCheckBox.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Left);
            rowCheckBox.SetBinding(ToggleButton.IsCheckedProperty, new MultiBinding
            {
                Converter = new CheckedConverter(),
                Bindings =
                {
                    new Binding
                    {
                        Path = new PropertyPath("."),
                        RelativeSource =
                            new RelativeSource(RelativeSourceMode.Self)
                    },
                    new Binding
                    {
                        Path = new PropertyPath("."),
                        RelativeSource =
                            new RelativeSource(RelativeSourceMode.FindAncestor) {AncestorType = typeof(DataGridRow)}
                    }
                },
                ConverterParameter = this
            });
            rowCheckBox.SetBinding(ButtonBase.CommandParameterProperty, new Binding
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor)
                {
                    AncestorType = typeof(DataGridRow)
                }
            });

            rowCheckBox.SetValue(ButtonBase.CommandProperty, CheckboxColumnCommand);

            HeaderButton.Command = new RelayCommand(_ =>
            {
                IsSelectAll = !IsSelectAll;

                foreach (var dataGridRow in DataGrid.GetRows())
                {
                    CheckedConverter.SetChecked(this, dataGridRow.Item, !IsSelectAll);
                    CheckboxColumnCommand.Execute(dataGridRow);
                }
            });

            DataGrid.Columns.Insert(0, new DataGridTemplateColumn
            {
                CellTemplate = new DataTemplate {VisualTree = rowCheckBox},
                Header = HeaderButton,
                MaxWidth = 48,
                CanUserResize = false,
                CanUserReorder = false
            });
        }

        private static void DynamicUsing(object resource, Action action)
        {
            try
            {
                action();
            }
            finally
            {
                if (resource is IDisposable d) d.Dispose();
            }
        }

        private static int? GetIEnumerableCount(IEnumerable enumerable)
        {
            switch (enumerable)
            {
                case null:
                    return null;
                case ICollection col:
                    return col.Count;
            }

            var c = 0;
            var e = enumerable.GetEnumerator();
            DynamicUsing(e, () =>
            {
                while (e.MoveNext()) c++;
            });

            return c;
        }

        public void AddColumnInterceptor(IColumnCreationInterceptor interceptor)
        {
            ColumnCreationInterceptors.Insert(0, interceptor);
            ReloadColumns();
        }

        public void RemoveColumnInterceptor(IColumnCreationInterceptor interceptor)
        {
            ColumnCreationInterceptors.Remove(interceptor);
            ReloadColumns();
        }

        public override void OnApplyTemplate()
        {
            PerPageComboBox = Template.FindName("PART_PerPage", this) as ComboBox;
            DataGrid = Template.FindName("PART_DataGrid", this) as FilteringDataGrid;

            if (DataGrid != null)
            {
                ((INotifyCollectionChanged) DataGrid.Items).CollectionChanged += OnCollectionChanged;
                DataGrid.AfterSorting += DataGridOnSorting;
            }

            SetupPerPageCombobox();
            SetupDataGrid();
        }

        private void DataGridOnSorting(object sender, EventArgs eventArgs)
        {
            UpdateSorting();

            var itemsBinding = BindingOperations.GetMultiBindingExpression(DataGrid, ItemsControl.ItemsSourceProperty);
            itemsBinding?.UpdateTarget();

            UpdateView();
        }

        private ICollectionView UpdateView()
        {
            var view = CollectionViewSource.GetDefaultView(ItemsSource);
            view.SortDescriptions.Clear();

            foreach (var sortDescription in cachedSortDescriptions)
            {
                view.SortDescriptions.Add(sortDescription);
                var column = DataGrid.Columns.FirstOrDefault(c => c.SortMemberPath == sortDescription.PropertyName);
                if (column != null) column.SortDirection = sortDescription.Direction;
            }

            cachedSortDescriptions.Clear();
            return view;
        }

        private void UpdateSorting()
        {
            var view = CollectionViewSource.GetDefaultView(DataGrid.ItemsSource);
            cachedSortDescriptions = new List<SortDescription>(view.SortDescriptions);
        }

        private void OnCollectionChanged(object o, NotifyCollectionChangedEventArgs notifyCollectionChangedEventArgs)
        {
            OnPropertyChanged(nameof(LastPage));
            OnPropertyChanged(nameof(TotalItems));
            OnPropertyChanged(nameof(CurrentMaxItem));
            OnPropertyChanged(nameof(CurrentMinItem));
            OnPropertyChanged(nameof(PaginationPageNumbers));
        }

        private void SetupDataGrid()
        {
            if (DataGrid != null)
            {
                ProtectedColumns = DataGrid.Columns.ToList();
                DataGrid.MouseDoubleClick += DataGridOnMouseDoubleClick;

                if (!HasCheckboxesColumn) return;

                DataGrid.MouseEnter += MouseEnterHandler;
            }
        }

        private void SetupPerPageCombobox()
        {
            if (PerPageComboBox.Items.Count > 0) return;

            PerPageComboBox?.Items.Add(1);

            for (var i = 5; i < 30; i += 5) PerPageComboBox?.Items.Add(i);

            if (PerPageComboBox != null)
                PerPageComboBox.SelectionChanged += (sender, args) =>
                {
                    IsSelectAll = false;
                    HandleCurrentPageOnMaxPagesChange();
                    BindingOperations.GetMultiBindingExpression(DataGrid, ItemsControl.ItemsSourceProperty)
                        ?.UpdateTarget();
                };
        }

        private static DependencyObject GetVisualParentByType(DependencyObject startObject, Type type)
        {
            var parent = startObject;
            while (parent != null)
            {
                if (type.IsInstanceOfType(parent)) break;

                parent = VisualTreeHelper.GetParent(parent);
            }

            return parent;
        }

        private static void MouseEnterHandler(object sender, MouseEventArgs e)
        {
            if (!(e.OriginalSource is DataGridRow row) || e.RightButton == MouseButtonState.Pressed) return;

            row.IsSelected = !row.IsSelected;
            e.Handled = true;
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj)
            where T : DependencyObject
        {
            if (depObj == null) yield break;

            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                var child = VisualTreeHelper.GetChild(depObj, i);
                if (child is T variable) yield return variable;

                foreach (var childOfChild in FindVisualChildren<T>(child)) yield return childOfChild;
            }
        }

        private void DataGridOnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.RightButton == MouseButtonState.Pressed) return;

            var cell = (DataGridCell) GetVisualParentByType(
                (FrameworkElement) e.OriginalSource, typeof(DataGridCell));

            if (cell != null && sender is DataGrid grid &&
                grid.SelectedItems.Count == 1)
                UpdateItemCommand.Execute(DataGrid.SelectedItem, DataGrid);
        }

        private void OnItemsSource(object collection)
        {
            ItemType = null;
            ViewSource.Source = collection;

            if (collection == null)
            {
                canMutate = false;
                ViewSource.Source = null;
                return;
            }

            var interfaces = collection
                .GetType()
                .GetInterfaces()
                .Where(t =>
                    t.IsGenericType &&
                    t.GetGenericTypeDefinition() == typeof(ICollection<>))
                .ToList();

            if (interfaces.Count > 1 || interfaces.Count == 0)
            {
                canMutate = false;
                ViewSource.Source = null;
                return;
            }

            if (collection is INotifyCollectionChanged notifyCollectionChanged)
                notifyCollectionChanged.CollectionChanged += NotifyCollectionChangedOnCollectionChanged;

            var collectionType = interfaces[0];
            ItemType = collectionType.GetGenericArguments()[0];
            canMutate = ItemType.GetConstructor(Type.EmptyTypes) != null;
        }

        private void NotifyCollectionChangedOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            ViewSource.Source = null;
            ViewSource.Source = sender;
        }

        private async void ExecuteCreateItem(object sender, ExecutedRoutedEventArgs e)
        {
            if (!canMutate) return;

            DialogResult result;
            var definition = GetCreateDefinition();
            try
            {
                DialogOptions.EnvironmentFlags.Add("create");
                result = await Show.Dialog(TargetDialogIdentifier, DataContext, DialogOptions).For(definition);
                DialogOptions.EnvironmentFlags.Remove("create");
            }
            catch
            {
                return;
            }

            if (result.Action is "DynamicDataGrid_CreateDialogPositive")
            {
                var collection = ItemsSource;

                ICreateActionContext context = new CreateActionContext(result.Model);
                foreach (var globalInterceptor in AddInterceptorChain)
                {
                    context = globalInterceptor.Intercept(context);
                    if (context == null) return;
                }

                if (!(collection is INotifyCollectionChanged) && DataGrid != null)
                {   
                    AddItemToCollection(ItemType, collection, context.NewModel);
                    ItemsSource = null;
                    ItemsSource = collection;
                }
                else
                {
                    AddItemToCollection(ItemType, collection, context.NewModel);
                }
            }
        }

        private void CanExecuteCreateItem(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = CanUserAdd && canMutate;
        }

        private async void ExecuteUpdateItem(object sender, ExecutedRoutedEventArgs e)
        {
            var model = e.Parameter;
            if (!canMutate || model == null || !ItemType.IsInstanceOfType(model)) return;

            DialogResult result;

            var definition = GetUpdateDefinition(model);
            try
            {
                DialogOptions.EnvironmentFlags.Add("update");
                result = await Show
                    .Dialog(TargetDialogIdentifier, DataContext, DialogOptions)
                    .For((IFormDefinition) definition);
                DialogOptions.EnvironmentFlags.Remove("update");
            }
            catch
            {
                return;
            }

            if (result.Action is "DynamicDataGrid_UpdateDialogNegative")
            {
                definition.Snapshot.Apply(model);
                return;
            }

            var oldModel = GetOldModel(definition);
            IUpdateActionContext context = new UpdateActionContext(oldModel, definition.Model);

            foreach (var globalInterceptor in UpdateInterceptorChain)
            {
                context = globalInterceptor.Intercept(context);
                if (context == null)
                    throw new InvalidOperationException(
                        $"{globalInterceptor.GetType().Name} are not allowed to return null.");
            }

            var contextDefinition = GetUpdateDefinition(context.NewModel);
            contextDefinition.Snapshot.Apply(model);
        }

        private static object GetOldModel(UpdateFormDefinition definition)
        {
            try
            {
                var oldModel = Activator.CreateInstance(definition.ModelType);
                definition.Snapshot.Apply(oldModel);
                return oldModel;
            }
            catch
            {
                // ignored
            }

            return null;
        }

        private void CanExecuteUpdateItem(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = CanUserEdit && canMutate && e.Parameter != null && ItemType.IsInstanceOfType(e.Parameter);
        }

        [SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
        private async void ExecuteRemoveItem(object sender, ExecutedRoutedEventArgs e)
        {
            void DoInterceptions(IRemoveActionContext context)
            {
                try
                {
                    foreach (var globalInterceptor in RemoveInterceptorChain) globalInterceptor.Intercept(context);
                }
                catch
                {
                    //supress
                }
            }

            var model = e.Parameter;
            if (!canMutate || model == null || !ItemType.IsInstanceOfType(e.Parameter))
                if (!(e.Parameter is IEnumerable enumerable &&
                      enumerable.Cast<object>().First().GetType() == ItemType))
                    return;

            try
            {
                DialogOptions.EnvironmentFlags.Add("delete");
                var result = await Show
                    .Dialog(TargetDialogIdentifier, DataContext, DialogOptions)
                    .For(new Confirmation(
                        RemoveDialogTextContent,
                        RemoveDialogTitleContent,
                        RemoveDialogPositiveContent,
                        RemoveDialogNegativeContent
                    )
                    {
                        PositiveActionIcon = RemoveDialogPositiveIcon,
                        NegativeActionIcon = RemoveDialogNegativeIcon
                    });
                DialogOptions.EnvironmentFlags.Remove("delete");

                if (result.Action is "positive")
                {
                    var collection = ItemsSource;

                    if (model is IEnumerable modelEnum)
                    {
                        foreach (var item in modelEnum.Cast<object>().ToList())
                        {
                            IRemoveActionContext context = new RemoveActionContext(item);
                            DoInterceptions(context);
                        }
                    }
                    else
                    {
                        IRemoveActionContext context = new RemoveActionContext(model);
                        DoInterceptions(context);
                    }

                    if (!(collection is INotifyCollectionChanged) && DataGrid != null)
                    {
                        ItemsSource = null;
                        RemoveItems(model, collection);
                        ItemsSource = collection;
                    }
                    else
                    {
                        RemoveItems(model, collection);
                    }

                    IsSelectAll = IsSelectAll && SelectedItemsCount > 0;
                    HeaderButton.IsChecked = IsSelectAll;
                }
            }
            catch
            {
                // ignored
            }
        }

        private void RemoveItems(object model, IEnumerable collection)
        {
            if (model is IEnumerable modelEnum)
                foreach (var item in modelEnum.Cast<object>().ToList())
                    RemoveItemFromCollection(ItemType, collection, item);
            else
                RemoveItemFromCollection(ItemType, collection, model);

            HandleCurrentPageOnMaxPagesChange();
        }

        private void HandleCurrentPageOnMaxPagesChange()
        {
            CurrentPage = Math.Min(LastPage, CurrentPage);
        }

        private void CanExecuteRemoveItem(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = CanUserRemove && canMutate && e.Parameter != null &&
                           (ItemType.IsInstanceOfType(e.Parameter) || e.Parameter is IEnumerable enumerable &&
                            enumerable.Cast<object>().Any() &&
                            enumerable.Cast<object>().First().GetType() == ItemType);
        }

        private IFormDefinition GetCreateDefinition()
        {
            var formDefinition = FormBuilder.GetDefinition(ItemType);
            return AddRows(formDefinition, new FormRow(true, 1)
            {
                Elements =
                {
                    new FormElementContainer(0, formDefinition.Grid.Length, new List<FormElement>
                    {
                        GetCreateNegativeAction().FreezeResources(),
                        GetCreatePositiveAction().FreezeResources()
                    })
                }
            });
        }

        private UpdateFormDefinition GetUpdateDefinition(object model)
        {
            var formDefinition = FormBuilder.GetDefinition(ItemType);
            return new UpdateFormDefinition(
                formDefinition,
                model,
                formDefinition.FormRows.Concat(
                    new[]
                    {
                        new FormRow(true, 1)
                        {
                            Elements =
                            {
                                new FormElementContainer(0, formDefinition.Grid.Length, new List<FormElement>
                                {
                                    GetUpdateNegativeAction().FreezeResources(),
                                    GetUpdatePositiveAction().FreezeResources()
                                })
                            }
                        }
                    }
                ).ToList().AsReadOnly()
            );
        }

        private ActionElement GetCreatePositiveAction()
        {
            return new ActionElement
            {
                Action = new LiteralValue("DynamicDataGrid_CreateDialogPositive"),
                Content = new LiteralValue(CreateDialogPositiveContent),
                Icon = new LiteralValue(CreateDialogPositiveIcon),
                ClosesDialog = LiteralValue.True,
                Validates = LiteralValue.True,
                IsDefault = LiteralValue.True
            };
        }

        private ActionElement GetCreateNegativeAction()
        {
            return new ActionElement
            {
                Action = new LiteralValue("DynamicDataGrid_CreateDialogNegative"),
                Content = new LiteralValue(CreateDialogNegativeContent),
                Icon = new LiteralValue(CreateDialogNegativeIcon),
                ClosesDialog = LiteralValue.True,
                IsCancel = LiteralValue.True
            };
        }

        private ActionElement GetUpdatePositiveAction()
        {
            return new ActionElement
            {
                Action = new LiteralValue("DynamicDataGrid_UpdateDialogPositive"),
                Content = new LiteralValue(UpdateDialogPositiveContent),
                Icon = new LiteralValue(UpdateDialogPositiveIcon),
                ClosesDialog = LiteralValue.True,
                Validates = LiteralValue.True,
                IsDefault = LiteralValue.True
            };
        }

        private ActionElement GetUpdateNegativeAction()
        {
            return new ActionElement
            {
                Action = new LiteralValue("DynamicDataGrid_UpdateDialogNegative"),
                Content = new LiteralValue(UpdateDialogNegativeContent),
                Icon = new LiteralValue(UpdateDialogNegativeIcon),
                ClosesDialog = LiteralValue.True,
                IsCancel = LiteralValue.True
            };
        }

        private static IFormDefinition AddRows(
            IFormDefinition formDefinition,
            params FormRow[] rows)
        {
            return new FormDefinitionWrapper(
                formDefinition,
                formDefinition.FormRows.Concat(rows ?? new FormRow[0]).ToList().AsReadOnly());
        }

        internal void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #region Dependency properties

        public string CreateDialogPositiveContent
        {
            get => (string) GetValue(CreateDialogPositiveContentProperty);
            set => SetValue(CreateDialogPositiveContentProperty, value);
        }

        public PackIconKind? CreateDialogPositiveIcon
        {
            get => (PackIconKind) GetValue(CreateDialogPositiveIconProperty);
            set => SetValue(CreateDialogPositiveIconProperty, value);
        }

        public string CreateDialogNegativeContent
        {
            get => (string) GetValue(CreateDialogNegativeContentProperty);
            set => SetValue(CreateDialogNegativeContentProperty, value);
        }

        public PackIconKind? CreateDialogNegativeIcon
        {
            get => (PackIconKind) GetValue(CreateDialogNegativeIconProperty);
            set => SetValue(CreateDialogNegativeIconProperty, value);
        }

        public string UpdateDialogPositiveContent
        {
            get => (string) GetValue(UpdateDialogPositiveContentProperty);
            set => SetValue(UpdateDialogPositiveContentProperty, value);
        }

        public PackIconKind? UpdateDialogPositiveIcon
        {
            get => (PackIconKind) GetValue(UpdateDialogPositiveIconProperty);
            set => SetValue(UpdateDialogPositiveIconProperty, value);
        }

        /// <summary>
        ///     Identifies the IsFilteringCaseSensitive dependency property.
        /// </summary>
        public static DependencyProperty IsFilteringCaseSensitiveProperty =
            DependencyProperty.Register("IsFilteringCaseSensitive", typeof(bool), typeof(DynamicDataGrid),
                new PropertyMetadata(true));

        public bool IsFilteringCaseSensitive
        {
            get => (bool) GetValue(IsFilteringCaseSensitiveProperty);
            set => SetValue(IsFilteringCaseSensitiveProperty, value);
        }

        public string UpdateDialogNegativeContent
        {
            get => (string) GetValue(UpdateDialogNegativeContentProperty);
            set => SetValue(UpdateDialogNegativeContentProperty, value);
        }

        public PackIconKind? UpdateDialogNegativeIcon
        {
            get => (PackIconKind) GetValue(UpdateDialogNegativeIconProperty);
            set => SetValue(UpdateDialogNegativeIconProperty, value);
        }

        public string RemoveDialogTitleContent
        {
            get => (string) GetValue(RemoveDialogTitleContentProperty);
            set => SetValue(RemoveDialogTitleContentProperty, value);
        }

        public string RemoveDialogTextContent
        {
            get => (string) GetValue(RemoveDialogTextContentProperty);
            set => SetValue(RemoveDialogTextContentProperty, value);
        }

        public string RemoveDialogPositiveContent
        {
            get => (string) GetValue(RemoveDialogPositiveContentProperty);
            set => SetValue(RemoveDialogPositiveContentProperty, value);
        }

        public PackIconKind? RemoveDialogPositiveIcon
        {
            get => (PackIconKind) GetValue(RemoveDialogPositiveIconProperty);
            set => SetValue(RemoveDialogPositiveIconProperty, value);
        }

        public string RemoveDialogNegativeContent
        {
            get => (string) GetValue(RemoveDialogNegativeContentProperty);
            set => SetValue(RemoveDialogNegativeContentProperty, value);
        }

        public PackIconKind? RemoveDialogNegativeIcon
        {
            get => (PackIconKind) GetValue(RemoveDialogNegativeIconProperty);
            set => SetValue(RemoveDialogNegativeIconProperty, value);
        }

        public IEnumerable ItemsSource
        {
            get => (IEnumerable) GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        public CollectionViewSource ViewSource { get; set; } = new CollectionViewSource();

        public IEnumerable<string> PaginationPageNumbers
        {
            get
            {
                const int EitherSide = 1;
                var range = new List<int>();
                var l = 0;

                range.Add(1);

                if (LastPage < 1 + EitherSide)
                {
                    yield return range.First().ToString();
                }
                else if (LastPage <= 5)
                {
                    foreach (var i in Enumerable.Range(1, LastPage)) yield return i.ToString();
                }
                else
                {
                    for (var i = CurrentPage - EitherSide; i <= CurrentPage + EitherSide; i++)
                        if (i < LastPage && i > 1)
                            range.Add(i);

                    range.Add(LastPage);

                    foreach (var i in range)
                    {
                        if (l != default(int))
                        {
                            if (i - l == 2)
                                yield return (l + 1).ToString();
                            else if (i - l != 1) yield return "...";
                        }

                        yield return i.ToString();
                        l = i;
                    }
                }
            }
        }

        private static void ItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((DynamicDataGrid) d).OnItemsSource(e.NewValue);
        }

        public DialogOptions DialogOptions
        {
            get => (DialogOptions) GetValue(DialogOptionsProperty);
            set => SetValue(DialogOptionsProperty, value);
        }

        public static readonly DependencyProperty FormBuilderProperty =
            DependencyProperty.Register(
                nameof(FormBuilder),
                typeof(IFormBuilder),
                typeof(DynamicDataGrid),
                new FrameworkPropertyMetadata(FormBuilding.FormBuilder.Default));

        public IFormBuilder FormBuilder
        {
            get => (IFormBuilder) GetValue(FormBuilderProperty);
            set => SetValue(FormBuilderProperty, value);
        }

        public static readonly DependencyProperty TargetDialogIdentifierProperty =
            DependencyProperty.Register(
                nameof(TargetDialogIdentifier),
                typeof(object),
                typeof(DynamicDataGrid),
                new FrameworkPropertyMetadata());

        public object TargetDialogIdentifier
        {
            get => GetValue(TargetDialogIdentifierProperty);
            set => SetValue(TargetDialogIdentifierProperty, value);
        }

        #endregion

        #region Collection helpers

        private static readonly Dictionary<Type, Action<object, object>> AddItemCache =
            new Dictionary<Type, Action<object, object>>();

        private static readonly Dictionary<Type, Action<object, object>> RemoveItemCache =
            new Dictionary<Type, Action<object, object>>();

        private RelayCommand CheckboxColumnCommand { get; }

        private static void AddItemToCollection(Type itemType, object collection, object item)
        {
            if (!AddItemCache.TryGetValue(itemType, out var action))
            {
                var collectionType = typeof(ICollection<>).MakeGenericType(itemType);
                var addMethod = collectionType.GetMethod("Add") ??
                                throw new InvalidOperationException("This should not happen.");
                var collectionParam = Expression.Parameter(typeof(object), "collection");
                var itemParam = Expression.Parameter(typeof(object), "item");
                var lambda = Expression.Lambda<Action<object, object>>(
                    Expression.Call(
                        Expression.Convert(collectionParam, collectionType),
                        addMethod,
                        Expression.Convert(itemParam, itemType)),
                    collectionParam,
                    itemParam
                );

                action = lambda.Compile();
                AddItemCache[itemType] = action;
            }

            action(collection, item);
        }

        private void RemoveItemFromCollection(Type itemType, object collection, object item)
        {
            if (!RemoveItemCache.TryGetValue(itemType, out var action))
            {
                var collectionType = typeof(ICollection<>).MakeGenericType(itemType);
                var removeMethod = collectionType.GetMethod("Remove") ??
                                   throw new InvalidOperationException("This should not happen.");
                var collectionParam = Expression.Parameter(typeof(object), "collection");
                var itemParam = Expression.Parameter(typeof(object), "item");
                var lambda = Expression.Lambda<Action<object, object>>(
                    Expression.Call(
                        Expression.Convert(collectionParam, collectionType),
                        removeMethod,
                        Expression.Convert(itemParam, itemType)),
                    collectionParam,
                    itemParam
                );

                action = lambda.Compile();
                RemoveItemCache[itemType] = action;
            }

            CheckedConverter.Remove(this, item);
            action(collection, item);
        }

        #endregion
    }
}