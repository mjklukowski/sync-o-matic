﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SyncOMatic.Networking
{
    public class SyncCoordinator
    {
        private static SyncCoordinator instance;
        private static readonly object instanceLock = new object();

        private List<SyncTaskBundle> syncQueue;
        private SyncTaskBundle syncAllBundle;

        private SyncCoordinator()
        {
            syncQueue = new List<SyncTaskBundle>();
        }

        public static SyncCoordinator GetInstance()
        {
            if (instance == null)
                lock (instanceLock)
                    if (instance == null)
                        instance = new SyncCoordinator();
            return instance;
        }

        public void SyncAll()
        {
            if (syncAllBundle != null)
                return;

            LoadSyncTasks();
            StartSync();
        }

        public async void StartSync()
        {
            if (syncQueue.Count > 0 && syncQueue[0].InProgress)
                return;

            while(syncQueue.Count > 0)
            {
                var queue = syncQueue[0];
                await queue.FetchFilesList();
                CompareFiles(queue);
                await queue.Run();
                foreach (var syncTask in queue.GetBundle)
                    syncTask.RemoteDevice.LastSync = DateTime.Now;

                syncQueue.Remove(queue);

                if (queue == syncAllBundle)
                    syncAllBundle = null;
            }
        } 

        private void LoadSyncTasks()
        {
            syncAllBundle = new SyncTaskBundle();
            
            foreach (var device in App.RemoteDevices)
            {
                if (!device.IsActive)
                    continue;

                foreach (var syncRule in device.SyncRules)
                {
                    if (!syncRule.IsActive)
                        continue;

                    var syncTask = new SyncTask(device, syncRule);
                    syncAllBundle.Add(syncTask);
                }
            }

            syncQueue.Add(syncAllBundle);
        }

        public void AddSyncBundle(SyncTaskBundle bundle)
        {
            syncQueue.Add(bundle);
        }

        private void CompareFiles(SyncTaskBundle queue)
        {
            var getSyncTasks = queue.GetBundle;

            for (int stIdx = 0; stIdx < getSyncTasks.Count; stIdx++)
            {
                var syncTask = getSyncTasks[stIdx];
                var files = syncTask.Files;

                // Go through each file in sync task
                for (int fIdx = files.Count - 1; fIdx >= 0; fIdx--)
                {
                    var filesPair = files[fIdx];
                    var localFile = filesPair.LocalFile;
                    var remoteFile = filesPair.RemoteFile;

                    if (localFile.ModifyTime > remoteFile.ModifyTime)
                        files.RemoveAt(fIdx);   // and exclude files that are up-to-date
                    else
                    {
                        // For a file that is old, search through the rest of sync task
                        for (int ostIdx = stIdx + 1; ostIdx < getSyncTasks.Count; ostIdx++)
                        {
                            var otherSyncTask = getSyncTasks[ostIdx];
                            var otherFiles = otherSyncTask.Files;

                            // Check each file pair
                            foreach (var otherFilesPair in otherFiles)
                            {
                                // to find one referring to the same local path
                                if (localFile.Path != otherFilesPair.LocalFile.Path)
                                    continue;

                                if (remoteFile.ModifyTime >= otherFilesPair.RemoteFile.ModifyTime)
                                    otherFiles.Remove(otherFilesPair); // Exclude duplicated pair
                                else
                                    files.Remove(filesPair);    // Exclude the older one
                                break;
                            }

                            // Break this loop if newer file was found; it will be checked later
                            if (!files.Contains(filesPair))
                                break;
                        }
                    }
                }
            }
        }

    }
}
