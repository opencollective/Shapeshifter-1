﻿namespace Shapeshifter.WindowsDesktop.Controls.Window.ViewModels
{
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Collections.Specialized;
	using System.ComponentModel;
	using System.Diagnostics.CodeAnalysis;
	using System.Linq;
	using System.Runtime.CompilerServices;
	using System.Threading;
	using System.Threading.Tasks;

	using Data.Actions.Interfaces;
	using Data.Interfaces;

	using Infrastructure.Events;

	using Interfaces;

	using Mediators.Interfaces;
	using Serilog;

	using Services.Clipboard.Interfaces;
	using Services.Screen;

	class UserInterfaceViewModel : 
		IUserInterfaceViewModel, 
		IDisposable
	{
		IClipboardDataControlPackage selectedElement;
		IAction selectedAction;

		ScreenInformation activeScreen;

		readonly SemaphoreSlim singlePasteLock;
		readonly SemaphoreSlim elementsModificationLock;

		readonly IClipboardUserInterfaceInteractionMediator clipboardUserInterfaceInteractionMediator;
		readonly ILogger logger;
		readonly IClipboardPersistenceService clipboardPersistenceService;
		readonly IEnumerable<IAction> actionCandidates;

		public event EventHandler<UserInterfaceShownEventArgument> UserInterfaceShown;
		public event EventHandler<UserInterfaceHiddenEventArgument> UserInterfaceHidden;
		public event EventHandler<UserInterfacePaneSwappedEventArgument> UserInterfacePaneSwapped;
		public event EventHandler<UserInterfaceDataControlAddedEventArgument> UserInterfaceDataControlAdded;

		public ObservableCollection<IClipboardDataControlPackage> Elements { get; }

		public ScreenInformation ActiveScreen
		{
			get => activeScreen;
			set
			{
				activeScreen = value;
				OnPropertyChanged();
			}
		}

		public IAction SelectedAction
		{
			get => selectedAction ?? SelectedElement?.Data?.Actions?.FirstOrDefault();
			set
			{
				selectedAction = value;
				OnPropertyChanged();
			}
		}

		public IClipboardDataControlPackage SelectedElement
		{
			get => selectedElement;
			set
			{
				selectedElement = value;
				OnPropertyChanged();
			}
		}

		[SuppressMessage("ReSharper", "SuggestBaseTypeForParameter")]
		public UserInterfaceViewModel(
			IClipboardUserInterfaceInteractionMediator clipboardUserInterfaceInteractionMediator,
			ILogger logger,
			IClipboardPersistenceService clipboardPersistenceService,
			IEnumerable<IAction> actionCandidates)
		{
			Elements = new ObservableCollection<IClipboardDataControlPackage>();

			singlePasteLock = new SemaphoreSlim(1);
			elementsModificationLock = new SemaphoreSlim(1);

			this.clipboardUserInterfaceInteractionMediator = clipboardUserInterfaceInteractionMediator;
			this.logger = logger;
			this.clipboardPersistenceService = clipboardPersistenceService;
			this.actionCandidates = actionCandidates;

			SetUpClipboardUserInterfaceInteractionMediator();
		}

		void SetUpClipboardUserInterfaceInteractionMediator()
		{
			clipboardUserInterfaceInteractionMediator.PackageAdded += MediatorPackageAdded;

			clipboardUserInterfaceInteractionMediator.PaneSwapped += ClipboardUserInterfaceInteractionMediator_PaneSwapped;

			clipboardUserInterfaceInteractionMediator.RemovedCurrentItem += ClipboardUserInterfaceInteractionMediator_RemovedCurrentItem;

			clipboardUserInterfaceInteractionMediator.UserInterfaceHidden += Mediator_UserInterfaceHidden;
			clipboardUserInterfaceInteractionMediator.UserInterfaceShown += Mediator_UserInterfaceShown;

			clipboardUserInterfaceInteractionMediator.PastePerformed += Mediator_PastePerformed;

			clipboardUserInterfaceInteractionMediator.SelectedNextItem += ClipboardUserInterfaceInteractionMediator_SelectedNextItem;
			clipboardUserInterfaceInteractionMediator.SelectedPreviousItem += ClipboardUserInterfaceInteractionMediator_SelectedPreviousItem;
		}

		async void ClipboardUserInterfaceInteractionMediator_RemovedCurrentItem(object sender, EventArgs e)
		{
			await RemoveCurrentElementAsync();
		}

		async Task RemoveCurrentElementAsync()
		{
			await elementsModificationLock.WaitAsync();
			try
			{
				var currentElement = SelectedElement;
				var currentIndex = Elements.IndexOf(currentElement);

				Elements.Remove(currentElement);

				if (Elements.Count == 0)
				{
					HideInterface();
				}
				else
				{
					var targetIndex = currentIndex == Elements.Count ? Elements.Count - 1 : currentIndex;
					SelectedElement = Elements.ElementAt(targetIndex);
				}

				if (currentElement != null)
				{
					if (await clipboardPersistenceService.IsPersistedAsync(currentElement.Data))
						await clipboardPersistenceService.DeletePackageAsync(currentElement.Data);
				}
			}
			finally
			{
				elementsModificationLock.Release();
			}
		}

		void ClipboardUserInterfaceInteractionMediator_PaneSwapped(object sender, EventArgs e)
		{
			var pane = clipboardUserInterfaceInteractionMediator.CurrentPane;
			OnUserInterfacePaneSwapped(new UserInterfacePaneSwappedEventArgument(pane));
		}

		void ClipboardUserInterfaceInteractionMediator_SelectedPreviousItem(
			object sender,
			EventArgs e)
		{
			switch (clipboardUserInterfaceInteractionMediator.CurrentPane)
			{
				case ClipboardUserInterfacePane.Actions:
					SelectedAction = GetNewSelectedElementAfterHandlingUpKey(SelectedElement.Data.Actions.ToList(), SelectedAction);
					break;

				case ClipboardUserInterfacePane.ClipboardPackages:
					SelectedElement = GetNewSelectedElementAfterHandlingUpKey(Elements, SelectedElement);
					break;

				default:
					throw new InvalidOperationException(
						"Unknown user interface pane.");
			}
		}

		void ClipboardUserInterfaceInteractionMediator_SelectedNextItem(
			object sender,
			EventArgs e)
		{
			switch (clipboardUserInterfaceInteractionMediator.CurrentPane)
			{
				case ClipboardUserInterfacePane.Actions:
					SelectedAction = GetNewSelectedElementAfterHandlingDownKey(SelectedElement.Data.Actions.ToList(), SelectedAction);
					break;

				case ClipboardUserInterfacePane.ClipboardPackages:
					SelectedElement = GetNewSelectedElementAfterHandlingDownKey(
						Elements,
						SelectedElement);
					break;

				default:
					throw new InvalidOperationException(
						"Unknown user interface pane.");
			}
		}

		async void Mediator_PastePerformed(
			object sender,
			PastePerformedEventArgument e)
		{
			await PerformPasteAsync();
		}

		async Task PerformPasteAsync()
		{
			if (SelectedAction != null)
			{
				await singlePasteLock.WaitAsync();
				try
				{
					clipboardUserInterfaceInteractionMediator.Disconnect();

					await SelectedAction.PerformAsync(SelectedElement.Data);
					if (!await clipboardPersistenceService.IsPersistedAsync(SelectedElement.Data))
						await MoveSelectedItemToTopAsync();
						
					clipboardUserInterfaceInteractionMediator.Connect();
				}
				finally
				{
					singlePasteLock.Release();
				}
			}
		}

		async Task MoveSelectedItemToTopAsync()
		{
			await elementsModificationLock.WaitAsync();
			try
			{
				var oldSelectedElement = SelectedElement;
				SelectedElement = null;

				Elements.Remove(oldSelectedElement);

				var clone = oldSelectedElement.Clone();
				Elements.Insert(await GetIndexToInsertNewItemAsync(), clone);

				SelectedElement = clone;
			}
			finally
			{
				elementsModificationLock.Release();
			}
		}

		async Task<int> GetIndexToInsertNewItemAsync()
		{
			for (var i = 0; i < Elements.Count; i++)
			{
				var element = Elements[i];
				if (!await clipboardPersistenceService.IsPersistedAsync(element.Data))
					return i;
			}

			return Elements.Count;
		}

		static T GetNewSelectedElementAfterHandlingUpKey<T>(
			IList<T> list,
			T selectedElement)
		{
			var indexToUse = list.IndexOf(selectedElement) - 1;
			if (indexToUse < 0)
				indexToUse = list.Count - 1;

			return list[indexToUse];
		}

		static T GetNewSelectedElementAfterHandlingDownKey<T>(
			IList<T> list,
			T selectedElement)
		{
			var indexToUse = list.IndexOf(selectedElement) + 1;
			if (indexToUse == list.Count)
				indexToUse = 0;

			return list[indexToUse];
		}

		async void Mediator_UserInterfaceShown(object sender, UserInterfaceShownEventArgument e)
		{
			if (Elements.Count == 0)
			{
				logger.Information("Did not show the UI because there are no clipboard elements in the list.");
				return;
			}

			if (SelectedElement.Data.Actions.Count == 0)
				return;
			
			UserInterfaceShown?.Invoke(this, e);
		}

		void Mediator_UserInterfaceHidden(object sender, UserInterfaceHiddenEventArgument e)
		{
			HideInterface();
		}

		void HideInterface()
		{
			UserInterfaceHidden?.Invoke(
				this,
				new UserInterfaceHiddenEventArgument());
		}

		async void MediatorPackageAdded(object sender, PackageEventArgument e)
		{
			await AddElementAsync(e.Package);
		}

		async Task AddElementAsync(IClipboardDataControlPackage package)
		{
			await elementsModificationLock.WaitAsync();
			try
			{
				Elements.Insert(await GetIndexToInsertNewItemAsync(), package);
				SelectedElement = package;

				package.Data.PopulateCompatibleActionsAsync(actionCandidates);
			}
			finally
			{
				elementsModificationLock.Release();
			}

			UserInterfaceDataControlAdded?.Invoke(this, new UserInterfaceDataControlAddedEventArgument(package));
		}

		public event PropertyChangedEventHandler PropertyChanged;

		protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		public void Dispose()
		{
			UnsubscribeUserInterfaceInteractionMediatorEvents();
		}

		void UnsubscribeUserInterfaceInteractionMediatorEvents()
		{
			clipboardUserInterfaceInteractionMediator.PackageAdded -= MediatorPackageAdded;

			clipboardUserInterfaceInteractionMediator.PaneSwapped -= ClipboardUserInterfaceInteractionMediator_PaneSwapped;

			clipboardUserInterfaceInteractionMediator.RemovedCurrentItem -= ClipboardUserInterfaceInteractionMediator_RemovedCurrentItem;

			clipboardUserInterfaceInteractionMediator.UserInterfaceHidden -= Mediator_UserInterfaceHidden;
			clipboardUserInterfaceInteractionMediator.UserInterfaceShown -= Mediator_UserInterfaceShown;

			clipboardUserInterfaceInteractionMediator.PastePerformed -= Mediator_PastePerformed;

			clipboardUserInterfaceInteractionMediator.SelectedNextItem -= ClipboardUserInterfaceInteractionMediator_SelectedNextItem;
			clipboardUserInterfaceInteractionMediator.SelectedPreviousItem -= ClipboardUserInterfaceInteractionMediator_SelectedPreviousItem;
		}

		protected virtual void OnUserInterfacePaneSwapped(UserInterfacePaneSwappedEventArgument e)
		{
			UserInterfacePaneSwapped?.Invoke(this, e);
		}
	}
}