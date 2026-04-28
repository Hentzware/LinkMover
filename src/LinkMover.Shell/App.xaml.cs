// Copyright (c) 2026 Henning Entz
// SPDX-License-Identifier: Apache-2.0 WITH LicenseRef-Commons-Clause
// See LICENSE for the full Apache 2.0 text and Commons Clause restriction.

using System.Windows;
using LinkMover.Analyzer;
using LinkMover.Core.Abstractions;
using LinkMover.Core.Services;
using LinkMover.Inventory;
using LinkMover.JunctionCreation;
using Prism.DryIoc;
using Prism.Ioc;
using Prism.Modularity;

namespace LinkMover.Shell;

public partial class App : PrismApplication
{
    protected override Window CreateShell()
    {
        var shell = Container.Resolve<MainWindow>();
        shell.DataContext = Container.Resolve<MainWindowViewModel>();
        return shell;
    }

    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.Register<MainWindowViewModel>();

        containerRegistry.RegisterSingleton<IFileSystemService, FileSystemService>();
        containerRegistry.RegisterSingleton<IJunctionService, JunctionService>();
        containerRegistry.RegisterSingleton<IPrivilegeService, PrivilegeService>();
        containerRegistry.RegisterSingleton<IRobocopyService, RobocopyService>();
        containerRegistry.RegisterSingleton<IVerificationService, VerificationService>();
        containerRegistry.RegisterSingleton<IRestartManagerService, RestartManagerService>();
        containerRegistry.RegisterSingleton<IDiskScanner, DiskScanner>();
    }

    protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
    {
        moduleCatalog.AddModule<JunctionCreationModule>();
        moduleCatalog.AddModule<InventoryModule>();
        moduleCatalog.AddModule<AnalyzerModule>();
    }
}
