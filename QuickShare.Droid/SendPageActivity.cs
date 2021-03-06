﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using QuickShare.TextTransfer;
using Plugin.FilePicker;
using Android.Provider;
using Android.Database;
using Com.Github.Angads25.Filepicker.Model;
using Com.Github.Angads25.Filepicker.View;
using QuickShare.FileTransfer;
using System.Threading;
using Plugin.DeviceInfo;
using QuickShare.Droid.Classes;
using Android.Views.Animations;
using Android.Util;
using QuickShare.Common;

namespace QuickShare.Droid
{
    [Activity(Label = "SendPageActivity", Name = "com.ghiasi.quickshare.sendpage")]
    public class SendPageActivity : Activity
    {
        TextView sendStatus, sendProgressPercent;
        ProgressBar sendProgress, sendProgressIndeterminate;

        public static readonly int PickImageId = 1000;

        internal static bool IsInitialized = false;

        bool sendingFile = true;
        CancellationTokenSource sendFileCancellationTokenSource = new CancellationTokenSource();

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            SetContentView(Resource.Layout.SendPage);

            string contentType = Intent.GetStringExtra("ContentType") ?? "Unknown";

            sendStatus = FindViewById<TextView>(Resource.Id.sendStatus);
            sendProgress = FindViewById<ProgressBar>(Resource.Id.sendProgress);
            sendProgressIndeterminate = FindViewById<ProgressBar>(Resource.Id.sendProgressIndeterminate);
            sendProgressPercent = FindViewById<TextView>(Resource.Id.sendProgressPercent);

            if (IsInitialized)
                return;
            IsInitialized = true;

            InitSpinner();

            ProcessRequest(contentType);

            Analytics.TrackPage("Send");
        }

        public override void OnBackPressed()
        {
            if (sendingFile)
            {
                sendFileCancellationTokenSource.Cancel();
            }

            base.OnBackPressed();
        }

        private void InitSpinner()
        {
            var rotation = AnimationUtils.LoadAnimation(this, Resource.Animation.rotate);
            rotation.FillAfter = true;
            sendProgressIndeterminate.StartAnimation(rotation);

            sendProgress.Visibility = ViewStates.Invisible;
            sendProgressPercent.Visibility = ViewStates.Invisible;
            sendProgressIndeterminate.Visibility = ViewStates.Visible;
        }

        private async void ProcessRequest(string contentType)
        {
            bool isFromShareTarget = false;

            switch (contentType)
            {
                case "Clipboard":
                    await SendText(ClipboardHelper.GetClipboardText(this));
                    break;
                case "Picture":
                    PickAndSendPicture();
                    break;
                case "File":
                    await PickAndSendFile();
                    break;
                case "Url":
                    await OpenUrl(ClipboardHelper.GetClipboardText(this));
                    break;
                case "Share_File":
                    isFromShareTarget = true;
                    await SendFiles(Common.ShareFiles);
                    break;
                case "Share_Url":
                    isFromShareTarget = true;
                    await OpenUrl(Common.ShareText);
                    break;
                case "Share_Text":
                    isFromShareTarget = true;
                    await SendText(Common.ShareText);
                    break;
                case "Unknown":
                default:
                    Analytics.TrackException($"SendPageActivity: Unknown contentType '{contentType}'.", false);
                    break;
            }

            if (isFromShareTarget)
                Analytics.TrackEvent("Send", "ShareTarget");
            else
                Analytics.TrackEvent("Send", "WithinApp");
        }

        private string GetRealPathFromURI(Android.Net.Uri contentURI)
        {
            ICursor cursor = ContentResolver.Query(contentURI, null, null, null, null);
            cursor.MoveToFirst();
            string documentId = cursor.GetString(0);
            documentId = documentId.Split(':')[1];
            cursor.Close();

            cursor = ContentResolver.Query(
            Android.Provider.MediaStore.Images.Media.ExternalContentUri,
            null, MediaStore.Images.Media.InterfaceConsts.Id + " = ? ", new[] { documentId }, null);
            cursor.MoveToFirst();
            string path = cursor.GetString(cursor.GetColumnIndex(MediaStore.Images.Media.InterfaceConsts.Data));
            cursor.Close();

            return path;
        }

        protected override async void OnActivityResult(int requestCode, Result resultCode, Intent data)
        {
            if (requestCode == PickImageId)
            {
                if ((resultCode == Result.Ok) && (data != null))
                {
                    InitSpinner();
                    sendStatus.Text = "Connecting...";

                    List<string> files = new List<string>();

                    ClipData clipData = data.ClipData;
                    if (clipData != null)
                    {
                        for (int i = 0; i < clipData.ItemCount; i++)
                        {
                            ClipData.Item item = clipData.GetItemAt(i);
                            var uri = item.Uri;
                            files.Add(FilePathHelper.GetPath(this, uri));
                        }
                    }
                    else
                    {
                        Android.Net.Uri uri = data.Data;
                        var file = FilePathHelper.GetPath(this, uri);
                        files.Add(file);
                    }

                    if (files.Count == 0)
                    {
                        Finish();
                        return;
                    }

                    await SendFiles(files.ToArray());
                }
                else
                {
                    Finish();
                    return;
                }
            }
        }

        private void PickAndSendPicture()
        {
            sendProgressIndeterminate.Visibility = ViewStates.Gone;
            sendStatus.Text = "";

            Intent getIntent = new Intent(Intent.ActionGetContent);
            getIntent.SetType("image/*");
            getIntent.PutExtra(Intent.ExtraAllowMultiple, true);
            getIntent.PutExtra(Intent.ExtraLocalOnly, true);

            Intent pickIntent = new Intent(Intent.ActionPick, Android.Provider.MediaStore.Images.Media.ExternalContentUri);
            pickIntent.SetType("image/*");
            pickIntent.PutExtra(Intent.ExtraAllowMultiple, true);
            pickIntent.PutExtra(Intent.ExtraLocalOnly, true);

            Intent chooserIntent = Intent.CreateChooser(getIntent, "Select Picture");
            chooserIntent.PutExtra(Intent.ExtraInitialIntents, new Intent[] { pickIntent });

            StartActivityForResult(chooserIntent, PickImageId);
        }

        private async Task PickAndSendFile()
        {
            //TODO: check permission.

            var filePickerTask = new TaskCompletionSource<string[]>();

            DialogProperties properties = new DialogProperties()
            {
                SelectionMode = DialogConfigs.MultiMode,
                SelectionType = DialogConfigs.FileSelect,
                Root = new Java.IO.File(DialogConfigs.DefaultDir),
                ErrorDir = new Java.IO.File(DialogConfigs.DefaultDir),
                Offset = new Java.IO.File(DialogConfigs.DefaultDir),
                Extensions = null
            };

            FilePickerDialog dialog = new FilePickerDialog(this, properties);
            dialog.SetTitle("Select files");
            dialog.DialogSelection += (ss, ee) =>
            {
                filePickerTask.TrySetResult(ee.P0);
            };
            dialog.DismissEvent += (ss, ee) =>
            {
                filePickerTask.TrySetResult(new string[] { });
            };
            dialog.Show();

            string[] files = await filePickerTask.Task;

            if (files.Length == 0)
            {
                Finish();
                return;
            }

            await SendFiles(files);
        }

        private async Task SendFiles(string[] files)
        {
            sendStatus.Text = "Connecting...";

            var result = await Common.PackageManager.Connect(Common.GetCurrentRemoteSystem(), false);

            if (result != QuickShare.Common.Rome.RomeAppServiceConnectionStatus.Success)
            {
                Analytics.TrackEvent("SendToWindows", "file", "Failed");
                sendStatus.Text = $"Connect failed. ({result.ToString()})";
                return;
            }

            //Fix Rome Android bug (receiver app service closes after 5 seconds in first connection)
            Common.PackageManager.CloseAppService();
            result = await Common.PackageManager.Connect(Common.GetCurrentRemoteSystem(), false);

            if (result != QuickShare.Common.Rome.RomeAppServiceConnectionStatus.Success)
            {
                Analytics.TrackEvent("SendToWindows", "file", "Failed");
                sendStatus.Text = $"Connect failed. ({result.ToString()})";
                return;
            }

            string sendingText = (files.Length == 1) ? "Sending file..." : "Sending files...";
            sendStatus.Text = "Preparing...";

            string message = "";
            FileTransferResult transferResult = FileTransferResult.Successful;
            
            using (FileSender fs = new FileSender(Common.GetCurrentRemoteSystem(),
                                                  new WebServerComponent.WebServerGenerator(),
                                                  Common.PackageManager,
                                                  FindMyIPAddresses(),
                                                  CrossDeviceInfo.Current.Model))
            {
                sendProgress.Max = 1;
                fs.FileTransferProgress += (ss, ee) =>
                {
                    if (ee.State == FileTransferState.Error)
                    {
                        transferResult = FileTransferResult.FailedOnSend;
                        message = ee.Message;
                    }
                    else
                    {
                        RunOnUiThread(() =>
                        {
                            sendStatus.Text = sendingText;

                            SetProgressBarValue((int)ee.CurrentPart, (int)ee.Total + 1);
                        });
                    }
                };

                if (files.Length == 0)
                {
                    sendStatus.Text = "No files.";
                    //ViewModel.ProgressIsIndeterminate = false;
                    return;
                }
                else if (files.Length == 1)
                {
                    await Task.Run(async () =>
                    {
                        sendingFile = true;
                        transferResult = await fs.SendFile(sendFileCancellationTokenSource.Token, new PCLStorage.FileSystemFile(files[0]));
                        sendingFile = false;
                    });
                }
                else
                {
                    await Task.Run(async () =>
                    {
                        sendingFile = true;
                        transferResult = await fs.SendFiles(sendFileCancellationTokenSource.Token, 
                            from x in files
                            select new PCLStorage.FileSystemFile(x), DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss") + "\\");
                        sendingFile = false;
                    });
                }

                sendProgress.Progress = sendProgress.Max;
            }

            Dictionary<string, object> vs = new Dictionary<string, object>
            {
                { "Receiver", "System" },
                { "FinishService", "FinishService" },
            };
            await Common.PackageManager.Send(vs);

            Common.PackageManager.CloseAppService();

            SetProgressBarValueToMax();

            if (transferResult != FileTransferResult.Successful)
            {
                Analytics.TrackEvent("SendToWindows", "file", transferResult.ToString());

                if (transferResult != FileTransferResult.Cancelled)
                {
                    sendStatus.Text = "Failed.";
                    System.Diagnostics.Debug.WriteLine("Send failed.\r\n\r\n" + message);
                
                    if (transferResult == FileTransferResult.FailedOnHandshake)
                    {
                        message = "Couldn't reach remote device.\r\n\r\n" +
                            "Make sure both devices are connected to the same Wi-Fi or LAN network.";
                    }

                    AlertDialog.Builder alert = new AlertDialog.Builder(this);
                    alert.SetTitle(message);
                    alert.SetPositiveButton("OK", (senderAlert, args) => { });
                    RunOnUiThread(() =>
                    {
                        alert.Show();
                    });
                }
            }
            else
            {
                sendStatus.Text = "Finished.";
                Analytics.TrackEvent("SendToWindows", "file", "Success");
            }
        }

        private IEnumerable<string> FindMyIPAddresses()
        {
            return NetworkHelper.GetLocalIPAddresses();
        }

        private async Task OpenUrl(string url)
        {
            sendStatus.Text = "Connecting...";
            var result = await Common.PackageManager.LaunchUri(new Uri(url), Common.GetCurrentRemoteSystem());

            SetProgressBarValueToMax();
            if (result == QuickShare.Common.Rome.RomeRemoteLaunchUriStatus.Success)
            {
                sendStatus.Text = "Finished.";
                Analytics.TrackEvent("SendToWindows", "launchUri", "Success");
            }
            else
            {
                sendStatus.Text = result.ToString();
                Analytics.TrackEvent("SendToWindows", "launchUri", "Failed");
            }
        }

        private async Task SendText(string text)
        {
            sendStatus.Text = "Connecting...";

            if (!(await Common.PackageManager.QuickClipboard(text, Common.GetCurrentRemoteSystem(), CrossDeviceInfo.Current.Model, "roamit://clipboard")))
            {

                var result = await Common.PackageManager.Connect(Common.GetCurrentRemoteSystem(), false);

                if (result != QuickShare.Common.Rome.RomeAppServiceConnectionStatus.Success)
                {
                    sendStatus.Text = $"Connect failed. ({result.ToString()})";
                    Analytics.TrackEvent("SendToWindows", "text", "Failed");
                    return;
                }

                //Fix Rome Android bug (receiver app service closes after 5 seconds in first connection)
                Common.PackageManager.CloseAppService();
                result = await Common.PackageManager.Connect(Common.GetCurrentRemoteSystem(), false);

                if (result != QuickShare.Common.Rome.RomeAppServiceConnectionStatus.Success)
                {
                    sendStatus.Text = $"Connect failed. ({result.ToString()})";
                    Analytics.TrackEvent("SendToWindows", "text", "Failed");
                    return;
                }

                TextSender textSender = new TextSender(Common.PackageManager, CrossDeviceInfo.Current.Model);

                textSender.TextSendProgress += (ee) =>
                {
                    SetProgressBarValue(ee.SentParts, ee.TotalParts);
                };

                sendStatus.Text = "Sending...";

                bool sendResult = await textSender.Send(text, ContentType.ClipboardContent);

                SetProgressBarValueToMax();

                if (!sendResult)
                {
                    sendStatus.Text = "Send failed.";
                    sendProgress.Progress = sendProgress.Max;
                    Analytics.TrackEvent("SendToWindows", "text", "Failed");
                    return;
                }

                Common.PackageManager.CloseAppService();
            }
            
            sendStatus.Text = "Finished.";
            SetProgressBarValueToMax();
            Analytics.TrackEvent("SendToWindows", "text", "Success");
        }

        private void SetProgressBarValue(int val, int max)
        {
            sendProgress.Max = max;
            sendProgress.Progress = val;

            int percent = (100 * val) / max;
            sendProgressPercent.Text = $"{percent}%";

            sendProgress.Visibility = ViewStates.Visible;
            sendProgressPercent.Visibility = ViewStates.Visible;
            sendProgressIndeterminate.Visibility = ViewStates.Invisible;
        }

        private void SetProgressBarValueToMax()
        {
            if (sendProgress.Max == 0)
                sendProgress.Max = 1;
            SetProgressBarValue(sendProgress.Max, sendProgress.Max);
        }
    }
}