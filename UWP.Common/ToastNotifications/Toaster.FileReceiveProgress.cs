﻿#undef NOTIFICATIONHANDLER_DEBUGINFO

using QuickShare.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace QuickShare.ToastNotifications
{
    public static partial class Toaster // :D
    {
        private static Dictionary<Guid, string> fileReceiveProgresses = new Dictionary<Guid, string>();

        public static void ShowFileReceiveProgressNotification(string hostName, double percent, double receivedSize, Guid guid)
        {
#if NOTIFICATIONHANDLER_DEBUGINFO
            System.Diagnostics.Debug.WriteLine("Notif" + percent);
#endif

            //return;

            if (DeviceInfo.SystemVersion >= DeviceInfo.CreatorsUpdate)
            {
                if (DeviceInfo.FormFactorType == DeviceInfo.DeviceFormFactorType.Phone)
                    ShowFileReceiveProgressNotificationCreatorsForPhone(hostName, percent, guid);
                else
                    ShowFileReceiveProgressNotificationCreators(hostName, percent, receivedSize == 0 ? "" : HelperClasses.StringFunctions.GetSizeString(receivedSize), guid);
            }
            else
            {
                ShowFileReceiveProgressNotificationPreCreators(hostName, percent, guid);
            }
        }

        /**
        static DateTime lastNotifTime = DateTime.MinValue;
        private static void ShowFileReceiveProgressNotificationPreCreators(string hostName, double percent, Guid guid)
        {
            string percentString = ((int)(Math.Round(100.0 * percent))).ToString() + "%";

            if ((fileReceiveProgresses.ContainsKey(guid)) && (fileReceiveProgresses[guid] == percentString))
                return;

            if (DateTime.Now - lastNotifTime < TimeSpan.FromSeconds(4))
                return;

            lastNotifTime = DateTime.Now;

            fileReceiveProgresses[guid] = percentString;

            string toastXml = Templates.BasicText.Replace("{title}", $"Receiving from {hostName}...")
                                                 .Replace("{subtitle}", percentString);

            var doc = new XmlDocument();
            doc.LoadXml(toastXml);

            var toast = new ToastNotification(doc);
            toast.SuppressPopup = true;
            toast.Tag = guid.ToString();

            if (ToastNotificationManager.History.GetHistory().FirstOrDefault(x => x.Tag == guid.ToString()) != null)
                ToastNotificationManager.History.Remove(guid.ToString());

            ToastNotificationManager.CreateToastNotifier().Show(toast);
        }
        /**/

        /**/
        private static void ShowFileReceiveProgressNotificationPreCreators(string hostName, double percent, Guid guid)
        {
            if (ToastNotificationManager.History.GetHistory().FirstOrDefault(x => x.Tag == guid.ToString()) != null)
                return;

            string toastXml = Templates.BasicText.Replace("{title}", $"Receiving from {hostName}...")
                                                 .Replace("{subtitle}", "Open the app to see transfer progress.")
                                                 .Replace("{argsLaunch}", "action=fileProgress");

            var doc = new XmlDocument();
            doc.LoadXml(toastXml);

            var toast = new ToastNotification(doc)
            {
                SuppressPopup = true,
                NotificationMirroring = NotificationMirroring.Disabled,
                Tag = guid.ToString()
            };

            ToastNotificationManager.CreateToastNotifier().Show(toast);
        }
        /**/


        private static void ShowFileReceiveProgressNotificationCreators(string hostName, double percent, string status, Guid guid)
        {
            if (ToastNotificationManager.History.GetHistory().FirstOrDefault(x => x.Tag == guid.ToString()) == null)
            {
                string toastXml = Templates.ProgressBar.Replace("{argsLaunch}", "action=fileProgress")
                                                       .Replace("{progressTitle}", "")
                                                       .Replace("{progressValueStringOverride}", "");

                var doc = new XmlDocument();
                doc.LoadXml(toastXml);

                var toast = new ToastNotification(doc)
                {
                    SuppressPopup = true,
                    NotificationMirroring = NotificationMirroring.Disabled,
                    Tag = guid.ToString()
                };

                ToastNotificationManager.CreateToastNotifier().Show(toast);
            }
            
            NotificationData data = new NotificationData();
            data.Values.Add("progressValue", percent < 0 ? "indeterminate" : percent.ToString());
            data.Values.Add("progressStatus", status);
            data.Values.Add("title", $"Receiving from {hostName}...");

            ToastNotificationManager.CreateToastNotifier().Update(data, guid.ToString());
        }

        private static void ShowFileReceiveProgressNotificationCreatorsForPhone(string hostName, double percent, Guid guid)
        {
            if (ToastNotificationManager.History.GetHistory().FirstOrDefault(x => x.Tag == guid.ToString()) == null)
            {
                string toastXml = Templates.BasicText.Replace("{argsLaunch}", "action=fileProgress");

                var doc = new XmlDocument();
                doc.LoadXml(toastXml);

                var toast = new ToastNotification(doc)
                {
                    SuppressPopup = true,
                    NotificationMirroring = NotificationMirroring.Disabled,
                    Tag = guid.ToString(),
                    Data = new NotificationData(new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>("title", "Receiving..."),
                                                                                         new KeyValuePair<string, string>("subtitle", guid.ToString()) })
                };

                ToastNotificationManager.CreateToastNotifier().Show(toast);
            }

            int percentR = ((int)(Math.Round(100.0 * percent)));
            string percentString = (percentR < 0) ? "Preparing..." : (percentR.ToString() + "%");

            NotificationData data = new NotificationData();
            data.Values.Add("subtitle", percentString);
            data.Values.Add("title", $"Receiving from {hostName}...");

            ToastNotificationManager.CreateToastNotifier().Update(data, guid.ToString());
        }
    }
}
