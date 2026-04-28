using LinkMover.Core.Navigation;
using LinkMover.JunctionCreation.ViewModels;
using LinkMover.JunctionCreation.Views;
using Prism.Ioc;
using Prism.Modularity;

namespace LinkMover.JunctionCreation;

public class JunctionCreationModule : IModule
{
    public void OnInitialized(IContainerProvider containerProvider) { }

    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterForNavigation<JunctionWizardHostView, JunctionWizardHostViewModel>(ViewNames.JunctionWizardHost);
    }
}
