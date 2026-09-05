import { Injectable } from '@angular/core';
import { CanActivate, ActivatedRouteSnapshot, Router } from '@angular/router';
import { AuthService, UserRole } from './auth.services';


@Injectable({
  providedIn: 'root'
})
export class AuthGuard implements CanActivate {

  constructor(
    private auth: AuthService,
    private router: Router
  ) { }

  canActivate(route: ActivatedRouteSnapshot): boolean {

    const role = route.data['role'] as UserRole;

    if (this.auth.hasRole(role)) {
      return true;
    }

    this.router.navigate(['/']);
    return false;
  }
}
