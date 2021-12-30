﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace SyncOMatic.Responses
{
    public class SharedSubfoldersResponse : IResponse
    {
        private IList<SharedFolder> sharedFolders;
        public IList<SharedSubfolder> Subfolders { get; private set; }
        private IEnumerator<SharedSubfolder> subfoldersEnum;
        private string remotePath;

        public SharedSubfoldersResponse()
        {
            Subfolders = new List<SharedSubfolder>();
        }

        public SharedSubfoldersResponse(IPAddress clientIp)
        {
            foreach (var device in App.RemoteDevices)
            {
                if (!device.IpAddress.Equals(clientIp))
                    continue;

                sharedFolders = device.SharedFolders;
                break;
            }
        }

        public void AppendToSend(byte[] data)
        {
            remotePath = Encoding.UTF8.GetString(data);

            // /Name/
            // /C/Program Files/Internet Explorer

            string[] remotePathTree = remotePath.Split("/");
            string remoteRoot = remotePathTree[1];  // index 0 is empty
            string relativePath = Path.Combine(remotePathTree.Skip(2).ToArray());
            foreach(var folder in sharedFolders)
            {
                if (folder.Name == remoteRoot)
                {
                    string localPath = Path.Combine(folder.Path, relativePath);
                    var sharedSubfolder = new SharedSubfolder();
                    sharedSubfolder.Path = localPath;
                    sharedSubfolder.LoadLocalSubfolders();
                    Subfolders = new List<SharedSubfolder>(sharedSubfolder.Subfolders);
                    subfoldersEnum = Subfolders.GetEnumerator();
                    break;
                }
            }
        }

        public byte[] GetData()
        {
            List<byte> data = new List<byte>();

            if (subfoldersEnum == null)
                return new byte[0];

            while (subfoldersEnum.MoveNext())
            {
                SharedSubfolder folder = subfoldersEnum.Current;

                data.AddRange(Encoding.UTF8.GetBytes($"{remotePath}/{folder.Name}"));
                folder.LoadLocalSubfolders();
                data.Add(Convert.ToByte(folder.IsEmpty));

                break;
            }

            return data.ToArray();
        }

        public void AppendData(byte[] data)
        {
            int length = data.Length;
            var sharedSubfolder = new SharedSubfolder();
            sharedSubfolder.Path = Encoding.UTF8.GetString(data.AsSpan().Slice(0, length - 1));
            sharedSubfolder.IsEmpty = Convert.ToBoolean(data[length - 1]);
            Subfolders.Add(sharedSubfolder);
        }
    }
}