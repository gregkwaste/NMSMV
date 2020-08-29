using System;
using System.Windows;
using System.Windows.Controls;
using MVCore.GMDL;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Markup;
using System.Collections.ObjectModel;

namespace WPFModelViewer
{

    public class ModelNode : INotifyPropertyChanged
    {
        public ObservableCollection<ModelNode> Children { get; set; }
        public Model mdl;
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

        public ModelNode(Model md)
        {
            Name = md.name;
            mdl = md;
            Children = new ObservableCollection<ModelNode>();
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
