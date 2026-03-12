export interface LoginRequest {
  userName: string;
  password: string;
}

export interface RegisterRequest {
  userName: string;
  email: string;
  password: string;
}

export interface LoginResponse {
  token: string;
  expiresAt: string;
  userName: string;
  roles: string[];
}

export interface AuthUser {
  userName: string;
  roles: string[];
}
