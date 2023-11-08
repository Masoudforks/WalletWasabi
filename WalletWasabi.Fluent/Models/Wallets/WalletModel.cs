using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Reactive.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Daemon;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.Wallets.Labels;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Models.Wallets;

public partial interface IWalletModel : INotifyPropertyChanged
{
}

[AutoInterface]
public partial class WalletModel : ReactiveObject
{
	private readonly Lazy<IWalletCoinjoinModel> _coinjoin;
	private readonly Lazy<IWalletCoinsModel> _coins;

	[AutoNotify] private bool _isLoggedIn;

	public WalletModel(Wallet wallet, IAmountProvider amountProvider)
	{
		Wallet = wallet;
		AmountProvider = amountProvider;

		Auth = new WalletAuthModel(this, Wallet);
		Loader = new WalletLoadWorkflow(Wallet);
		Settings = new WalletSettingsModel(wallet.Id, Wallet.KeyManager);

		_coinjoin = new(() => new WalletCoinjoinModel(Wallet, Settings));
		_coins = new(() => new WalletCoinsModel(wallet, this));

		Transactions = new WalletTransactionsModel(this, wallet);

		AddressesModel = new AddressesModel(Transactions.TransactionProcessed, Wallet.KeyManager);

		State =
			Observable.FromEventPattern<WalletState>(Wallet, nameof(Wallet.StateChanged))
					  .ObserveOn(RxApp.MainThreadScheduler)
					  .Select(_ => Wallet.State);

		Privacy = new WalletPrivacyModel(this, Wallet);

		Balances = Transactions.TransactionProcessed
			.Select(_ => Wallet.Coins.TotalAmount())
			.Select(AmountProvider.Create);

		HasBalance = Balances.Select(x => x.HasBalance);

		// Start the Loader after wallet is logged in
		this.WhenAnyValue(x => x.Auth.IsLoggedIn)
			.Where(x => x)
			.Take(1)
			.Do(_ => Loader.Start())
			.Subscribe();

		// Stop the loader after load is completed
		State.Where(x => x == WalletState.Started)
			 .Do(_ => Loader.Stop())
			 .Subscribe();

		this.WhenAnyValue(x => x.Auth.IsLoggedIn).BindTo(this, x => x.IsLoggedIn);
	}

	public IAddressesModel AddressesModel { get; }

	internal Wallet Wallet { get; }

	public int Id => Wallet.Id;

	public string Name
	{
		get => Wallet.WalletName;
		set
		{
			if (value == null)
			{
				throw new InvalidOperationException("Wallet name can't be set to null");
			}

			if (!IsValidWalletName(value))
			{
				Logger.LogWarning($"Invalid name '{value}' when attempting to rename {Wallet.WalletName}");
				throw new InvalidOperationException($"Invalid name {value}");
			}

			var newName = value + "." + WalletDirectories.WalletFileExtension;
			var oldName = Wallet.WalletName + "." + WalletDirectories.WalletFileExtension;
			Rename(oldName, newName, Services.WalletManager.WalletDirectories.WalletsDir);
			try
			{
				Rename(oldName, newName, Services.WalletManager.WalletDirectories.WalletsBackupDir);
			}
			catch (Exception e)
			{
				Logger.LogWarning($"Could not rename wallet backup file. Reason: {e.Message}");
			}

			Wallet.WalletName = value;
			
			this.RaisePropertyChanged();
		}
	}

	private static bool IsValidWalletName(string value)
	{
		return !WalletHelpers.ValidateWalletName(value).HasValue;
	}

	private void Rename(string oldName, string newName, string rootDir)
	{
		File.Move(Path.Combine(rootDir, oldName), Path.Combine(rootDir, newName));
	}


	public Network Network => Wallet.Network;

	public IWalletTransactionsModel Transactions { get; }

	public IObservable<Amount> Balances { get; }

	public IObservable<bool> HasBalance { get; }

	public IWalletCoinsModel Coins => _coins.Value;

	public IWalletAuthModel Auth { get; }

	public IWalletLoadWorkflow Loader { get; }

	public IWalletSettingsModel Settings { get; }

	public IWalletPrivacyModel Privacy { get; }

	public IWalletCoinjoinModel Coinjoin => _coinjoin.Value;

	public IObservable<WalletState> State { get; }

	public IAmountProvider AmountProvider { get; }

	public IAddress GetNextReceiveAddress(IEnumerable<string> destinationLabels)
	{
		var pubKey = Wallet.GetNextReceiveAddress(destinationLabels);
		return new Address(Wallet.KeyManager, pubKey);
	}

	public IWalletInfoModel GetWalletInfo()
	{
		return new WalletInfoModel(Wallet);
	}

	public IWalletStatsModel GetWalletStats()
	{
		return new WalletStatsModel(this, Wallet);
	}

	public bool IsHardwareWallet => Wallet.KeyManager.IsHardwareWallet;

	public bool IsWatchOnlyWallet => Wallet.KeyManager.IsWatchOnly;

	public IEnumerable<(string Label, int Score)> GetMostUsedLabels(Intent intent)
	{
		return Wallet.GetLabelsWithRanking(intent);
	}
}
