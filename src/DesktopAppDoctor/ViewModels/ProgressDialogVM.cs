using Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DesktopAppDoctor.Views;

namespace DesktopAppDoctor.ViewModels
{
    public class ProgressDialogVM : BindableBase
    {
        private string _title = "任务进行中";
        private string _content = "请稍后";
        private string _content2 = "请稍后";
        private int _maxProgressValue = 100;
        private int _currenProgressValue = 0;
        private bool _isIndeterminate = true;
        private string _cancelButtonContent = "取消";

        public bool CanClose { get; set; } = false;

        public string Title { get => _title; set => SetProperty(ref _title, value); }
        public string Content { get => _content; set => SetProperty(ref _content, value); }
        public string Content2 { get => _content2; set => SetProperty(ref _content2, value); }
        public int MaxProgressValue { get => _maxProgressValue; set => SetProperty(ref _maxProgressValue, value); }
        public int CurrenProgressValue { get => _currenProgressValue; set => SetProperty(ref _currenProgressValue, value); }
        public bool IsIndeterminate { get => _isIndeterminate; set => SetProperty(ref _isIndeterminate, value); }
        public string CancelButtonContent { get => _cancelButtonContent; set => SetProperty(ref _cancelButtonContent, value); }
        public Action<ProgressDialog> StartHandler { get; set; }
        public Action<ProgressDialog> CancelHandler { get; set; }
        public Task Task { get; set; }
    }
}
