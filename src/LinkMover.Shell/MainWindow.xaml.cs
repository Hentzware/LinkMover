// Copyright (c) 2026 Henning Entz
// SPDX-License-Identifier: Apache-2.0 WITH LicenseRef-Commons-Clause
// See LICENSE for the full Apache 2.0 text and Commons Clause restriction.

using System.Windows;
using MahApps.Metro.Controls;

namespace LinkMover.Shell;

public partial class MainWindow : MetroWindow
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnAboutClick(object sender, RoutedEventArgs e)
    {
        var dialog = new AboutDialog { Owner = this };
        dialog.ShowDialog();
    }
}
