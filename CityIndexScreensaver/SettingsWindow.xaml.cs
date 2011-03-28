﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

using CityIndexScreensaver.Utils;

using CIAPI.DTO;

namespace CityIndexScreensaver
{
	/// <summary>
	/// Interaction logic for SettingsWindow.xaml
	/// </summary>
	public partial class SettingsWindow : Window
	{
		public SettingsWindow()
		{
			InitializeComponent();
		}

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			Application.Current.DispatcherUnhandledException += OnUnhandledException;

			this.HideMinimizeAndMaximizeButtons();

			State.Data = new Data(ReportException);
			State.Data.GetMarketsList(RefreshMarketsView);

			DataContext = ApplicationSettings.Instance;
		}

		private void RefreshMarketsView(MarketDTO[] markets)
		{
			_marketsView = CollectionViewSource.GetDefaultView(markets);
			AllMarketsGrid.ItemsSource = _marketsView;

			var marketNames = markets.ToDictionary(market => market.MarketId, market => market.Name);

			_subscriptions.Clear();
			foreach (var id in ApplicationSettings.Instance.PricesToWatch)
			{
				string marketName;
				if (marketNames.TryGetValue(id, out marketName))
				{
					_subscriptions.Add(new MarketDTO { MarketId = id, Name = marketName });
				}
			}

			SubscriptionsGrid.ItemsSource = _subscriptions;
		}

		static void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs args)
		{
			try
			{
				ReportException(args.Exception);
			}
			catch (Exception exc)
			{
				Debugger.Break();
			}

			args.Handled = true;
		}

		private static void ReportException(Exception exc)
		{
			var msg = State.IsDebug ? exc.ToString() : exc.Message;
			MessageBox.Show(msg);
		}

		private void OKButton_Click(object sender, RoutedEventArgs e)
		{
			var subscribedIds = _subscriptions.Select(subscription => subscription.MarketId).ToArray();
			ApplicationSettings.Instance.PricesToWatch = subscribedIds;

			if (!string.IsNullOrEmpty(PasswordEdit.Password))
				ApplicationSettings.Instance.Password = PasswordEdit.Password;

			ApplicationSettings.Instance.Save();
			this.DialogResult = true;
		}

		private void CancelButton_Click(object sender, RoutedEventArgs e)
		{
			// ApplicationSettings.Reload(); // not necessary, coz the app will terminate after settings window closed
			this.DialogResult = false;
		}

		private void ImageAdd_Click(object sender, MouseButtonEventArgs e)
		{
			var items = AllMarketsGrid.SelectedItems;
			foreach (MarketDTO market in items)
			{
				var newSubscription = new MarketDTO { MarketId = market.MarketId, Name = market.Name };

				for (var i = 0; i < _subscriptions.Count; i++)
				{
					var cur = _subscriptions[i];
					if (cur.MarketId == newSubscription.MarketId)
					{
						_subscriptions.RemoveAt(i);
						break;
					}
				}
				_subscriptions.Add(newSubscription);
			}
		}

		private void ImageRemove_Click(object sender, MouseButtonEventArgs e)
		{
			var cell = (FrameworkElement)e.OriginalSource;
			var item = (MarketDTO)cell.DataContext;

			var i = _subscriptions.IndexOf(item);
			_subscriptions.RemoveAt(i);
			SubscriptionsGrid.SelectedIndex = i;
		}

		private void SubscriptionsGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			e.Handled = true;
			SubscriptionsGrid.Focus();

			if (_isDragging)
				return;

			var cell = e.OriginalSource as FrameworkElement;
			if (cell == null)
				return;

			var source = cell.DataContext as MarketDTO;
			if (source == null)
				return;

			_draggingItem = source;
			_isDragging = true;

			SubscriptionsGrid.SelectedIndex = _subscriptions.IndexOf(_draggingItem);
		}

		private void SubscriptionsGrid_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			SubscriptionsGrid.Focus();
			_isDragging = false;
		}

		// DataGrid mouse move events are suppressed, when left mouse button is pressed
		// so, subscribe to mouse move events of the parent control instead
		private void SubscriptionsGrid_PreviewMouseMove(object sender, MouseEventArgs e)
		{
			if (!_isDragging)
				return;

			var text = e.OriginalSource as TextBlock;
			if (text == null)
				return;

			var current = (MarketDTO)text.DataContext;
			if (current.MarketId != _draggingItem.MarketId)
			{
				var curIndex = _subscriptions.IndexOf(current);
				var sourceIndex = _subscriptions.IndexOf(_draggingItem);
				_subscriptions.Move(sourceIndex, curIndex);

				SubscriptionsGrid.SelectedIndex = curIndex;
			}
		}

		private void FilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			_marketsView.Filter =
				val =>
				{
					var market = (MarketDTO)val;
					var res = market.Name.ToLower().Contains(FilterTextBox.Text.ToLower());
					return res;
				};
		}

		private void FilterTextBox_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Escape)
			{
				FilterTextBox.Text = "";
				e.Handled = true;
			}
		}

		private ICollectionView _marketsView;
		private readonly ObservableCollection<MarketDTO> _subscriptions = new ObservableCollection<MarketDTO>();

		private bool _isDragging;
		private MarketDTO _draggingItem;
	}
}
