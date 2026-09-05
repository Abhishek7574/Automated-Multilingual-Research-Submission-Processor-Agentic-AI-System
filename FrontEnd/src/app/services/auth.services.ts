import { Injectable } from '@angular/core';
import { Router } from '@angular/router';

export type UserRole = 'admin' | 'user';

interface Credentials {
  username: string;
  password: string;
  role: UserRole;
}

const STATIC_USERS: Credentials[] = [
  { username: 'admin', password: 'admin', role: 'admin' },
  { username: 'user', password: 'user', role: 'user' }
];

@Injectable({
  providedIn: 'root'
})
export class AuthService {

  private currentRole: UserRole | null = null;
  private currentUser: string | null = null;

  constructor(private router: Router) {
    // Restore session from localStorage (optional but recommended)
    const savedRole = localStorage.getItem('role') as UserRole | null;
    const savedUser = localStorage.getItem('user');

    if (savedRole && savedUser) {
      this.currentRole = savedRole;
      this.currentUser = savedUser;
    }
  }

  // ============================
  // Login
  // ============================
  login(username: string, password: string): boolean {

    const match = STATIC_USERS.find(
      u => u.username === username && u.password === password
    );

    if (match) {
      this.currentRole = match.role;
      this.currentUser = match.username;

      localStorage.setItem('role', match.role);
      localStorage.setItem('user', match.username);

      this.router.navigate([`/${match.role}`]);
      return true;
    }

    return false;
  }

  // ============================
  // Login with specific role
  // ============================
  loginAsRole(username: string, password: string, role: UserRole): boolean {

    const match = STATIC_USERS.find(
      u => u.username === username &&
        u.password === password &&
        u.role === role
    );

    if (match) {
      this.currentRole = match.role;
      this.currentUser = match.username;

      localStorage.setItem('role', match.role);
      localStorage.setItem('user', match.username);

      this.router.navigate([`/${match.role}`]);
      return true;
    }

    return false;
  }

  // ============================
  // Logout
  // ============================
  logout(): void {
    this.currentRole = null;
    this.currentUser = null;

    localStorage.removeItem('role');
    localStorage.removeItem('user');

    this.router.navigate(['/']);
  }

  // ============================
  // Helpers
  // ============================
  isLoggedIn(): boolean {
    return this.currentRole !== null;
  }

  hasRole(role: UserRole): boolean {
    return this.currentRole === role;
  }

  getCurrentRole(): UserRole | null {
    return this.currentRole;
  }

  getCurrentUser(): string | null {
    return this.currentUser;
  }
}
