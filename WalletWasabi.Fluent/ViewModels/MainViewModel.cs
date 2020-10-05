#nullable enable
using Avalonia.Threading;
using NBitcoin;
using ReactiveUI;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Fluent.ViewModels.Dialog;
using Global = WalletWasabi.Gui.Global;

namespace WalletWasabi.Fluent.ViewModels
{
	public class MainViewModel : ViewModelBase, IScreen, IDialogHost
	{
		private Global _global;
		private StatusBarViewModel _statusBar;
		private string _title = "Wasabi Wallet";
		private DialogViewModelBase? _currentDialog;
		private NavBarViewModel _navBar;
		private bool _isDialogOpen;

		public MainViewModel(Global global)
		{
			_global = global;

			Network = global.Network;

			StatusBar = new StatusBarViewModel(global.DataDir, global.Network, global.Config, global.HostedServices, global.BitcoinStore.SmartHeaderChain, global.Synchronizer, global.LegalDocuments);

			NavBar = new NavBarViewModel(this, Router, global.WalletManager, global.UiConfig);
		}

		public static MainViewModel Instance { get; internal set; }

		public RoutingState Router { get; } = new RoutingState();

		public ReactiveCommand<Unit, IRoutableViewModel> GoNext { get; }

		public ReactiveCommand<Unit, Unit> GoBack => Router.NavigateBack;

		public Network Network { get; }

		DialogViewModelBase? IDialogHost.CurrentDialog
		{
			get => _currentDialog;
			set => this.RaiseAndSetIfChanged(ref _currentDialog, value);
		}
		
		public NavBarViewModel NavBar
		{
			get => _navBar;
			set => this.RaiseAndSetIfChanged(ref _navBar, value);
		}

		public StatusBarViewModel StatusBar
		{
			get => _statusBar;
			set => this.RaiseAndSetIfChanged(ref _statusBar, value);
		}

		public string Title
		{
			get => _title;
			internal set => this.RaiseAndSetIfChanged(ref _title, value);
		}

		bool IDialogHost.IsDialogOpen
		{
			get => _isDialogOpen;
			set => this.RaiseAndSetIfChanged(ref _isDialogOpen, value);
		}

		public void Initialize()
		{
			// Temporary to keep things running without VM modifications.
			MainWindowViewModel.Instance = new MainWindowViewModel(_global.Network, _global.UiConfig, _global.WalletManager, null, null, false);

			StatusBar.Initialize(_global.Nodes.ConnectedNodes);

			if (Network != Network.Main)
			{
				Title += $" - {Network}";
			}
		}

		void IDialogHost.ShowDialog<TDialog>(TDialog dialogViewModel)
		{
			var dialogHost = (this as IDialogHost);
			dialogHost.CurrentDialog = dialogViewModel;
			dialogHost.IsDialogOpen = true;
		}

		void IDialogHost.CloseDialog()
		{
			(this as IDialogHost).IsDialogOpen = false;
			// (this as IDialogHost).CurrentDialog = null;
		}
	}
}
