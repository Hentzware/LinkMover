// Copyright (c) 2026 Henning Entz
// SPDX-License-Identifier: Apache-2.0 WITH LicenseRef-Commons-Clause
// See LICENSE for the full Apache 2.0 text and Commons Clause restriction.

using LinkMover.Core.Navigation;
using LinkMover.Inventory.Restore;
using LinkMover.Inventory.Services;
using LinkMover.Inventory.ViewModels;
using LinkMover.Inventory.Views;
using Prism.Ioc;
using Prism.Modularity;

namespace LinkMover.Inventory;

public class InventoryModule : IModule
{
    public void OnInitialized(IContainerProvider containerProvider) { }

    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<IInventoryScanner, InventoryScanner>();
        containerRegistry.RegisterSingleton<IJunctionRestoreOrchestrator, JunctionRestoreOrchestrator>();

        containerRegistry.RegisterForNavigation<InventoryListView, InventoryListViewModel>(ViewNames.InventoryList);
    }
}
