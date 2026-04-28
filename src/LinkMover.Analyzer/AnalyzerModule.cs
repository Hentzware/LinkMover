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
