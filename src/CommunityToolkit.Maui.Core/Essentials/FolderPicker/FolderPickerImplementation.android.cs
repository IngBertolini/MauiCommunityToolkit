using System.Web;

using Android.Content;
using Android.Provider;

using CommunityToolkit.Maui.Core.Primitives;

using Microsoft.Maui.ApplicationModel;

using PortableStorage.Droid;

using AndroidUri = Android.Net.Uri;

namespace CommunityToolkit.Maui.Storage;

/// <inheritdoc />
public sealed partial class FolderPickerImplementation : IFolderPicker
{
	async Task<Folder> InternalPickAsync(string initialPath, CancellationToken cancellationToken)
	{
		if (!OperatingSystem.IsAndroidVersionAtLeast(29))
		{
			var status = await Permissions.RequestAsync<Permissions.StorageRead>().WaitAsync(cancellationToken).ConfigureAwait(false);
			if (status is not PermissionStatus.Granted)
			{
				throw new PermissionException("Storage permission is not granted.");
			}
		}

		Folder? folder = null;
		const string baseUrl = "content://com.android.externalstorage.documents/document/primary%3A";
		if (Android.OS.Environment.ExternalStorageDirectory is not null)
		{
			initialPath = initialPath.Replace(Android.OS.Environment.ExternalStorageDirectory.AbsolutePath, string.Empty, StringComparison.InvariantCulture);
		}

		var initialFolderUri = AndroidUri.Parse(baseUrl + HttpUtility.UrlEncode(initialPath));

		var intent = new Intent(Intent.ActionOpenDocumentTree);
		intent.PutExtra(DocumentsContract.ExtraInitialUri, initialFolderUri);
		intent.PutExtra("android.content.extra.SHOW_ADVANCED", value: true);
		intent.PutExtra("android.content.extra.FANCY", value: true);
		var pickerIntent = Intent.CreateChooser(intent, string.Empty) ?? throw new InvalidOperationException("Unable to create intent.");

		await IntermediateActivity.StartAsync(pickerIntent, (int) AndroidRequestCode.RequestCodeFolderPicker, onResult: OnResult).WaitAsync(cancellationToken);

		return folder ?? throw new FolderPickerException("Unable to get folder.");

		void OnResult(Intent resultIntent)
		{
			var uri = SafStorageHelper.ResolveFromActivityResult(Platform.CurrentActivity, resultIntent);
			using var storage = SafStorgeProvider.CreateStorage(Platform.CurrentActivity, uri);
			folder = new Folder(AndroidUri.Decode(storage.Uri.OriginalString)!, storage.Name);
		}
	}

	Task<Folder> InternalPickAsync(CancellationToken cancellationToken)
	{
		return InternalPickAsync(GetExternalDirectory(), cancellationToken);
	}

	static string GetExternalDirectory()
	{
		return Android.OS.Environment.ExternalStorageDirectory?.Path ?? "/storage/emulated/0";
	}

	static string EnsurePhysicalPath(AndroidUri? uri)
	{
		if (uri is null)
		{
			throw new FolderPickerException("Path is not selected.");
		}

		const string uriSchemeFolder = "content";
		if (uri.Scheme is not null && uri.Scheme.Equals(uriSchemeFolder, StringComparison.OrdinalIgnoreCase))
		{
			var split = uri.Path?.Split(':') ?? throw new FolderPickerException("Unable to resolve path.");
			return $"{Android.OS.Environment.ExternalStorageDirectory}/{split[^1]}";
		}

		throw new FolderPickerException($"Unable to resolve absolute path or retrieve contents of URI '{uri}'.");
	}
}
