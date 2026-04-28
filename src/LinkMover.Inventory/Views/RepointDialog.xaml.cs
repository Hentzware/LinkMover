// Copyright (c) 2026 Henning Entz
// SPDX-License-Identifier: Apache-2.0 WITH LicenseRef-Commons-Clause
// See LICENSE for the full Apache 2.0 text and Commons Clause restriction.

using System.Windows;
using LinkMover.Inventory.ViewModels;

namespace LinkMover.Inventory.Views;

public partial class RepointDialog : Window
{
    public RepointDialog()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is RepointDialogViewModel oldVm)
        {
            oldVm.RequestClose -= OnRequestClose;
        }
        if (e.NewValue is RepointDialogViewModel newVm)
        {
            newVm.RequestClose += OnRequestClose;
        }
    }

    private void OnRequestClose(bool result)
    {
        DialogResult = result;
        Close();
    }
}
