using System.Collections.Generic;
using ConnectApp.models;

namespace ConnectApp.redux.actions {
    public class FetchEventsAction : RequestAction {
        public int pageNumber = 0;
        public string tab;
    }

    public class FetchEventsSuccessAction : BaseAction {
        public List<IEvent> events;
        public int pageNumber = 0;
        public string tab;
    }

    public class FetchEventDetailAction : RequestAction {
        public string eventId;
    }

    public class FetchEventDetailSuccessAction : BaseAction {
        public IEvent eventObj;
    }

    public class GetEventHistoryAction : BaseAction {
    }

    public class SaveEventHistoryAction : BaseAction {
        public IEvent eventObj;
    }

    public class DeleteEventHistoryAction : BaseAction {
        public string eventId;
    }

    public class DeleteAllEventHistoryAction : BaseAction {
    }

    public class JoinEventAction : RequestAction {
        public string eventId;
    }

    public class JoinEventSuccessAction : BaseAction {
        public string eventId;
    }

    public class ClearEventDetailAction : BaseAction {
    }
}