﻿using System.Windows.Forms;

namespace ArDrone2.WinApp
{
    public static class TreeViewHelper
    {
        public static TreeNode GetOrCreate(this TreeNodeCollection nodes, string key)
        {
            if (nodes.ContainsKey(key) == false)
                return nodes.Add(key, key);
            return nodes[key];
        }
    }
}