using System;
using strange.extensions.injector.api;

namespace ScienceAlert
{
    class TemporaryBinding : ITemporaryBinding
    {
        private readonly IInjectionBinder _binder;

        private readonly bool _mustUnbind;

        public Type BoundType { get; private set; }

        public TemporaryBinding(IInjectionBinder binder, Type binding, bool mustUnbind)
        {
            if (binder == null) throw new ArgumentNullException("binder");
            if (binding == null) throw new ArgumentNullException("binding");
            _binder = binder;
            _mustUnbind = mustUnbind;
            BoundType = binding;
        }


        private void Dispose(bool disposing)
        {
            if (!disposing) return;

            if (_mustUnbind)
                _binder.Unbind(BoundType);
            GC.SuppressFinalize(this);
        }


        public void Dispose()
        {
            Dispose(true);
        }


        public object GetInstance()
        {
            return _binder.GetInstance(BoundType);
        }
    }
}