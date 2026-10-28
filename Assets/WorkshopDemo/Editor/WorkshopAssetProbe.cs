using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace WorkshopDemo.Editor
{
    public static class WorkshopAssetProbe
    {
        static UnityWebRequest request;
        static string token;
        static int step;
        static double deadline;
        static readonly string[] endpoints = {
            "https://packages.unity.com/-/api/product/344832",
            "https://packages.unity.com/-/api/legacy-package-download-info/344832"
        };

        public static void Run()
        {
            step = 0;
            Directory.CreateDirectory("Evidence");
            try
            {
                var type = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.Connect.UnityConnect");
                var instance = type.GetProperty("instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
                token = (string)type.GetMethod("GetAccessToken").Invoke(instance, null);
                File.WriteAllText("Evidence/asset-acquisition.txt",
                    "Factory Training (344832), official Unity package service.\n" +
                    "Editor login available: " + !string.IsNullOrEmpty(token) + "\n");
                deadline = EditorApplication.timeSinceStartup + 90;
                Next();
                EditorApplication.update += Tick;
            }
            catch (Exception e)
            {
                File.WriteAllText("Evidence/asset-acquisition.txt", "Asset probe failed: " + e.GetType().Name);
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
        }

        static void Next()
        {
            request = UnityWebRequest.Get(endpoints[step]);
            request.timeout = 25;
            if (!string.IsNullOrEmpty(token)) request.SetRequestHeader("Authorization", "Bearer " + token);
            request.SendWebRequest();
        }

        static void Tick()
        {
            if (!request.isDone && EditorApplication.timeSinceStartup < deadline) return;
            File.AppendAllText("Evidence/asset-acquisition.txt", endpoints[step] + ": HTTP " + request.responseCode + "\n");
            if (request.responseCode == 200)
                File.WriteAllText("Evidence/asset-service-" + step + ".json", request.downloadHandler.text);
            request.Dispose();
            if (++step < endpoints.Length) Next();
            else
            {
                token = null;
                EditorApplication.update -= Tick;
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
        }
    }
}
