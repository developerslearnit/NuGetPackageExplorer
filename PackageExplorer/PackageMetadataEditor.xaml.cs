﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using NuGet.Packaging;
using NuGetPackageExplorer.Types;
using NuGetPe;
using PackageExplorerViewModel;

namespace PackageExplorer
{

    public partial class PackageMetadataEditor : UserControl, IPackageEditorService
    {
        private ObservableCollection<FrameworkAssemblyReference> _frameworkAssemblies;
        private EditableFrameworkAssemblyReference _newFrameworkAssembly;
        private ICollection<PackageDependencyGroup> _dependencySets;
        private ICollection<PackageReferenceSet> _referenceSets;

        public PackageMetadataEditor()
        {
            InitializeComponent();
            PopulateLanguagesForLanguageBox();
            PopulateFrameworkAssemblyNames();

            // Explicitly set the data context for these since they don't flow
            NewAssemblyName.DataContext = NewSupportedFramework.DataContext = null;
        }

        public IUIServices UIServices { get; set; }
        public IPackageChooser PackageChooser { get; set; }

        private void PackageMetadataEditor_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (Visibility == Visibility.Visible)
            {
                ClearFrameworkAssemblyTextBox();
                PrepareBindings();
            }
        }

        private void PrepareBindings()
        {
            var viewModel = (PackageViewModel)DataContext;

            _dependencySets = viewModel.PackageMetadata.DependencySets;
            _referenceSets = viewModel.PackageMetadata.PackageAssemblyReferences;

            _frameworkAssemblies = new ObservableCollection<FrameworkAssemblyReference>(viewModel.PackageMetadata.FrameworkAssemblies);
            FrameworkAssembliesList.ItemsSource = _frameworkAssemblies;
        }

        private void ClearFrameworkAssemblyTextBox()
        {
            _newFrameworkAssembly = new EditableFrameworkAssemblyReference();
            NewAssemblyName.DataContext = NewSupportedFramework.DataContext = _newFrameworkAssembly;
        }

        private void PopulateLanguagesForLanguageBox()
        {
            LanguageBox.ItemsSource =
                CultureInfo.GetCultures(CultureTypes.SpecificCultures)
                    .Select(c => c.Name)
                    .OrderBy(p => p, StringComparer.OrdinalIgnoreCase);
        }

        private void PopulateFrameworkAssemblyNames()
        {
            const string fxAssemblyPath = "Resources/fxAssemblies.txt";
            if (File.Exists(fxAssemblyPath))
            {
                try
                {
                    NewAssemblyName.ItemsSource = File.ReadAllLines(fxAssemblyPath);
                }
                catch (Exception)
                {
                    // ignore exception
                }
            }
        }

        private void RemoveFrameworkAssemblyButtonClicked(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            var item = (FrameworkAssemblyReference)button.DataContext;

            _frameworkAssemblies.Remove(item);
        }

        private void AddFrameworkAssemblyButtonClicked(object sender, RoutedEventArgs args)
        {
            AddPendingFrameworkAssembly();
        }

        // return true = pending assembly added
        // return false = pending assembly is invalid
        // return null = no pending asesmbly
        private bool? AddPendingFrameworkAssembly()
        {
            // Blank assembly name but content in the supported framework textbox is an error
            if (string.IsNullOrWhiteSpace(NewAssemblyName.Text) && !string.IsNullOrWhiteSpace(NewSupportedFramework.Text))
            {
                return false;
            }

            // blank in both is ok, nothing to add
            if (string.IsNullOrWhiteSpace(NewAssemblyName.Text))
            {
                return null;
            }

            if (!NewFrameworkAssembly.UpdateSources())
            {
                return false;
            }
            _frameworkAssemblies.Add(_newFrameworkAssembly.AsReadOnly());

            // after framework assembly is added, clear the textbox
            ClearFrameworkAssemblyTextBox();

            return true;
        }

        private void EditDependenciesButtonClicked(object sender, RoutedEventArgs e)
        {
            var editor = new PackageDependencyEditor(_dependencySets)
            {
                Owner = Window.GetWindow(this),
                PackageChooser = PackageChooser
            };
            var result = editor.ShowDialog();
            if (result == true)
            {
                _dependencySets = editor.GetEditedDependencySets();
            }
        }

        private void EditReferencesButtonClicked(object sender, RoutedEventArgs e)
        {
            var editor = new PackageReferencesEditor(_referenceSets)
            {
                Owner = Window.GetWindow(this),
                PackageChooser = PackageChooser
            };
            var result = editor.ShowDialog();
            if (result == true)
            {
                _referenceSets = editor.GetEditedReferencesSets();
            }
        }

        #region IPackageEditorService

        void IPackageEditorService.BeginEdit()
        {
            PackageMetadataGroup.BeginEdit();
        }

        void IPackageEditorService.CancelEdit()
        {
            PackageMetadataGroup.CancelEdit();
        }

        bool IPackageEditorService.CommitEdit()
        {
            var addPendingAssembly = AddPendingFrameworkAssembly();
            if (addPendingAssembly == false)
            {
                return false;
            }

            var valid = PackageMetadataGroup.CommitEdit();
            if (valid)
            {
                var viewModel = (PackageViewModel)DataContext;
                viewModel.PackageMetadata.DependencySets = _dependencySets;
                viewModel.PackageMetadata.PackageAssemblyReferences = _referenceSets;
                _frameworkAssemblies.CopyTo(viewModel.PackageMetadata.FrameworkAssemblies);
            }

            return valid;
        }

        #endregion
    }
}
