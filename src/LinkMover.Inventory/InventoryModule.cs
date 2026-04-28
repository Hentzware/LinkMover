using LinkMover.Core.Navigation;
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
        containerRegistry.RegisterForNavigation<InventoryListView, InventoryListViewModel>(ViewNames.InventoryList);
    }
}
