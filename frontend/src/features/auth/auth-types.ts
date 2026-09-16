export type CurrentUser = {
  id: string;
  email: string;
  displayName: string;
};

export type SessionResponse = {
  accessToken: string;
  accessTokenExpiresAt: string;
  user: CurrentUser;
};
