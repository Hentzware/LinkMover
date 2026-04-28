// Copyright (c) 2026 Henning Entz
// SPDX-License-Identifier: Apache-2.0 WITH LicenseRef-Commons-Clause
// See LICENSE for the full Apache 2.0 text and Commons Clause restriction.

using LinkMover.Analyzer.ViewModels;
using LinkMover.Analyzer.Views;
using LinkMover.Core.Navigation;
using Prism.Ioc;
using Prism.Modularity;

namespace LinkMover.Analyzer;

public class AnalyzerModule : IModule
{
    public void OnInitialized(IContainerProvider containerProvider) { }

    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterForNavigation<AnalyzerHomeView, AnalyzerHomeViewModel>(ViewNames.AnalyzerHome);
    }
}
