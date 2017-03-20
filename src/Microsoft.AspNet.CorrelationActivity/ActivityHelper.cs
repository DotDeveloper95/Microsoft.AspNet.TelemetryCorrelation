﻿using System;
using System.Diagnostics;
using System.Web;

namespace Microsoft.AspNet.CorrelationActivity
{
    /// <summary>
    /// Activity helper class
    /// </summary>
    internal static class ActivityHelper
    {
        public const string AspNetListenerName = "Microsoft.AspNet.Correlation";
        public const string AspNetActivityName = "Microsoft.AspNet.Activity";
        public const string AspNetActivityStartName = "Microsoft.AspNet.Activity.Start";
        public const string AspNetExceptionActivityName = "Microsoft.AspNet.Activity.Exception";

        public const string ActivityKey = "__AspnetActivity__";
        private static DiagnosticListener s_aspNetListener = new DiagnosticListener(AspNetListenerName);

        /// <summary>
        /// It's possible that a request is executed in both native threads and managed threads,
        /// in such case Activity.Current will be lost during native thread and managed thread swtich.
        /// This method is intended to restore the current activity in order to correlate the child
        /// activities with the root activity of the request.
        /// </summary>
        /// <returns>If it returns an activity, the dev is responsible for stopping it</returns>
        public static Activity RestoreCurrentActivity(HttpContextBase context)
        {
            if(Activity.Current != null || context == null ||
               context.Items[ActivityKey] as Activity == null)
            {
                return null;
            }

            // workaround to restore the root activity, because we don't
            // have a way to change the Activity.Current
            var root = (Activity)context.Items[ActivityKey];
            var childActivity = new Activity(root.OperationName);
            childActivity.SetParentId(root.Id);
            foreach(var item in root.Baggage)
            {
                childActivity.AddBaggage(item.Key, item.Value);
            }
            childActivity.Start();

            return childActivity;
        }

        /// <summary>
        /// To stop the activity that dev gets from RestoreCurrentActivity
        /// </summary>
        /// <param name="activity"></param>
        /// <param name="context"></param>
        public static void StopAspNetActivity(Activity activity, object context)
        {
            if (activity != null)
            {
                s_aspNetListener.StopActivity(activity, context);
            }
        }

        public static Activity CreateRootActivity(HttpContextBase context)
        {
            Activity rootActivity = null;
            if (s_aspNetListener.IsEnabled() && s_aspNetListener.IsEnabled(AspNetActivityName))
            {
                rootActivity = new Activity(ActivityHelper.AspNetActivityName);

                rootActivity.RestoreActivityInfoFromRequestHeaders(context.Request.Headers);
                StartAspNetActivity(rootActivity, new { Context = context });
                SaveCurrentActivity(context, rootActivity);
            }

            return rootActivity;
        }

        public static void TriggerAspNetExceptionActivity(Exception ex)
        {
            if(s_aspNetListener.IsEnabled() && s_aspNetListener.IsEnabled(AspNetExceptionActivityName))
            {
                s_aspNetListener.Write(AspNetExceptionActivityName, new { ActivityException = ex });
            }
        }

        private static void StartAspNetActivity(Activity activity, object context)
        {
            if (s_aspNetListener.IsEnabled(AspNetActivityName, activity, context))
            {
                if (s_aspNetListener.IsEnabled(AspNetActivityStartName))
                {
                    s_aspNetListener.StartActivity(activity, context);
                }
                else
                {
                    activity.Start();
                }
            }
        }

        /// <summary>
        /// This should be called after the Activity starts
        /// and only for root activity of a request
        /// </summary>
        private static void SaveCurrentActivity(HttpContextBase context, Activity activity)
        {
            Debug.Assert(context != null);
            Debug.Assert(activity != null);
            Debug.Assert(context.Items[ActivityKey] == null);

            context.Items[ActivityKey] = activity;
        }
    }
}
