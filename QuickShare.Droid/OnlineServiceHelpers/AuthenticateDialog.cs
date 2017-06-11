﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.Webkit;
using System.Threading.Tasks;

namespace QuickShare.Droid.OnlineServiceHelpers
{
    internal static class AuthenticateDialog
    {
        private static WebView webView;
        internal static Dialog authDialog;
        internal static TaskCompletionSource<MsaAuthResult> authenticateTcs;

        internal static async Task<MsaAuthResult> ShowAsync(Context context, MsaAuthPurpose purpose)
        {
            if (purpose == MsaAuthPurpose.App)
            {
                string url = "https://login.live.com/oauth20_authorize.srf?redirect_uri=https://login.live.com/oauth20_desktop.srf&response_type=code&client_id=" + Config.Secrets.ClientId2 + "&scope=User.Read+wl.offline_access";
                return await ShowAsync(context, purpose, url);
            }
            else
                throw new InvalidOperationException("AuthenticateDialog.Show() can't determine url");
        }

        internal static async Task<MsaAuthResult> ShowAsync(Context context, MsaAuthPurpose purpose, string oauthUrl)
        {
            authenticateTcs = new TaskCompletionSource<MsaAuthResult>();

            authDialog = new Dialog(context);

            authDialog.CancelEvent += (ss, ee) => 
            {
                authenticateTcs.TrySetResult(MsaAuthResult.CancelledByUser);
            };

            var linearLayout = new LinearLayout(authDialog.Context);
            webView = new WebView(authDialog.Context);
            linearLayout.AddView(webView);
            authDialog.SetContentView(linearLayout);

            webView.SetWebChromeClient(new WebChromeClient());
            webView.Settings.JavaScriptEnabled = true;
            webView.Settings.DomStorageEnabled = true;
            webView.LoadUrl(oauthUrl);

            webView.SetWebViewClient(new MsaWebViewClient(context, purpose));
            authDialog.Show();
            authDialog.SetCancelable(true);

            return await authenticateTcs.Task;
        }
    }
}