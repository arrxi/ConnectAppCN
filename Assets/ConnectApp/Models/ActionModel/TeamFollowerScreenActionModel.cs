using System;
using RSG;

namespace ConnectApp.Models.ActionModel {
    public class TeamFollowerScreenActionModel : BaseActionModel {
        public Action<string> pushToPersonalDetail;
        public Action startFetchFollower;
        public Func<int, IPromise> fetchFollower;
        public Action<string> startFollowUser;
        public Func<string, IPromise> followUser;
        public Action<string> startUnFollowUser;
        public Func<string, IPromise> unFollowUser;
    }
}