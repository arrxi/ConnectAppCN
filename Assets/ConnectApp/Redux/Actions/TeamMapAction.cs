using System.Collections.Generic;
using ConnectApp.Api;
using ConnectApp.Models.Model;
using ConnectApp.Models.State;
using Unity.UIWidgets.Redux;
using UnityEngine;

namespace ConnectApp.redux.actions {
    public class TeamMapAction : BaseAction {
        public Dictionary<string, Team> teamMap;
    }

    public class StartFetchTeamAction : RequestAction {
    }

    public class FetchTeamSuccessAction : BaseAction {
        public Team team;
        public string teamId;
    }

    public class FetchTeamFailureAction : BaseAction {
    }

    public class StartFetchTeamArticleAction : RequestAction {
    }

    public class FetchTeamArticleSuccessAction : BaseAction {
        public List<Article> articles;
        public bool hasMore;
        public int offset;
        public string teamId;
    }

    public class FetchTeamArticleFailureAction : BaseAction {
    }

    public class StartFetchTeamFollowerAction : RequestAction {
    }

    public class FetchTeamFollowerSuccessAction : BaseAction {
        public List<User> followers;
        public bool followersHasMore;
        public int offset;
        public string teamId;
    }

    public class FetchTeamFollowerFailureAction : BaseAction {
    }

    public static partial class Actions {
        public static object fetchTeam(string teamId) {
            return new ThunkAction<AppState>((dispatcher, getState) => {
                return TeamApi.FetchTeam(teamId)
                    .Then(teamResponse => {
                        dispatcher.dispatch(new StartFetchTeamArticleAction());
                        dispatcher.dispatch(fetchTeamArticle(teamId, 0));
                        if (teamResponse.placeMap != null) {
                            dispatcher.dispatch(new PlaceMapAction {placeMap = teamResponse.placeMap});
                        }
                        dispatcher.dispatch(new FetchTeamSuccessAction {
                            team = teamResponse.team,
                            teamId = teamId
                        });
                    })
                    .Catch(error => {
                        dispatcher.dispatch(new FetchTeamFailureAction());
                        Debug.Log(error);
                    }
                );
            });
        }

        public static object fetchTeamArticle(string teamId, int offset) {
            return new ThunkAction<AppState>((dispatcher, getState) => {
                return TeamApi.FetchTeamArticle(teamId, offset)
                    .Then(teamArticleResponse => {
                        dispatcher.dispatch(new FetchTeamArticleSuccessAction {
                            articles = teamArticleResponse.projects,
                            hasMore = teamArticleResponse.projectsHasMore,
                            offset = offset,
                            teamId = teamId
                        });
                    })
                    .Catch(error => {
                            dispatcher.dispatch(new FetchTeamArticleFailureAction());
                            Debug.Log(error);
                        }
                    );
            });
        }

        public static object fetchTeamFollower(string teamId, int offset) {
            return new ThunkAction<AppState>((dispatcher, getState) => {
                return TeamApi.FetchTeamFollower(teamId, offset)
                    .Then(teamFollowerResponse => {
                        if (teamFollowerResponse.followMap != null) {
                            dispatcher.dispatch(new FollowMapAction {
                                followMap = teamFollowerResponse.followMap,
                                userId = getState().loginState.loginInfo.userId ?? ""
                            });
                        }
                        dispatcher.dispatch(new FetchTeamFollowerSuccessAction {
                            followers = teamFollowerResponse.followers,
                            followersHasMore = teamFollowerResponse.followersHasMore,
                            offset = offset,
                            teamId = teamId
                        });
                    })
                    .Catch(error => {
                            dispatcher.dispatch(new FetchTeamFollowerFailureAction());
                            Debug.Log(error);
                        }
                    );
            });
        }
    }
}