﻿namespace RGFS.RGFlt
{
    public class RGFltFileInfo
    {
        public RGFltFileInfo(string name, long size, bool isFolder)
        {
            this.Name = name;
            this.Size = size;
            this.IsFolder = isFolder;
        }

        public string Name { get; }
        public long Size { get; }
        public bool IsFolder { get; }        
    }
}
