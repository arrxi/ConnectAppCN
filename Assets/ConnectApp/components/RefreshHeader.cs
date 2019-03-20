using ConnectApp.components.refresh;
using Unity.UIWidgets.foundation;
using Unity.UIWidgets.widgets;

namespace ConnectApp.components {
    public class RefreshHeader : RefreshChild {
        public RefreshHeader(
            RefreshWidgetController controller,
            Key key = null
        ) : base(controller, key) {
        }

        public override State createState() {
            return new _RefreshHeaderState();
        }
    }

    internal class _RefreshHeaderState : State<RefreshHeader> {
        private RefreshState _state = RefreshState.drag;

        public override void didUpdateWidget(StatefulWidget oldWidget) {
            if (oldWidget is RefreshHeader newWidget)
                if (widget.controller != newWidget.controller) {
                    newWidget.controller.removeStateListener(_updateState);
                    widget.controller.addStateListener(_updateState);
                }

            base.didUpdateWidget(oldWidget);
        }

        public override void didChangeDependencies() {
            widget.controller.addStateListener(_updateState);
            base.didChangeDependencies();
        }

        public override void dispose() {
            widget.controller.removeStateListener(_updateState);
            base.dispose();
        }

        private void _updateState() {
            setState(() => { _state = widget.controller.state; });
        }

        public override Widget build(BuildContext context) {
            var animatingType = _state == RefreshState.loading ? AnimatingType.repeat : AnimatingType.stop;
            return new Container(
                child: new CustomActivityIndicator(
                    animating: animatingType,
                    size: 32
                )
            );
        }
    }
}