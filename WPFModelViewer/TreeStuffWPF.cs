using System;
using System.Windows;
using System.Windows.Controls;
using MVCore.GMDL;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Markup;

namespace WPFModelViewer
{

    public class ModelNode : INotifyPropertyChanged
    {
        public List<ModelNode> Children { get; set; }
        public model mdl;
        private bool _isChecked;
        public bool IsInitiallySelected { get; }
        public string Name { set; get; } = "";

        public bool IsChecked
        {
            get { return _isChecked; }

            set
            {
                _isChecked = value;
                mdl.renderable = value;
                foreach (var child in Children)
                    child.IsChecked = value;
                NotifyPropertyChanged("isChecked");
            }
        }

        public ModelNode(model md)
        {
            Name = md.name;
            mdl = md;
            Children = new List<ModelNode>();
            IsInitiallySelected = true;
            IsChecked = true;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged(String info)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(info));
        }

    }
}
