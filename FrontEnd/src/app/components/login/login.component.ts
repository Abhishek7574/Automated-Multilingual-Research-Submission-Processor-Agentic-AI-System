import { Component, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { UserRole, AuthService } from '../../services/auth.services';


@Component({
  selector: 'app-login',
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.css']
})
export class LoginComponent implements OnInit {

  username: string = '';
  password: string = '';
  selectedRole: UserRole = 'user';
  errorMessage: string = '';

  constructor(
    private auth: AuthService,
    private route: ActivatedRoute
  ) { }

  ngOnInit(): void {
    const role = this.route.snapshot.queryParamMap.get('role');
    if (role === 'admin' || role === 'user') {
      this.selectedRole = role;
    }
  }

  onRoleChange(role: UserRole): void {
    this.selectedRole = role;
    this.username = '';
    this.password = '';
    this.errorMessage = '';
  }

  onSubmit(): void {
    const success = this.auth.loginAsRole(
      this.username.trim(),
      this.password,
      this.selectedRole
    );

    if (!success) {
      const label = this.selectedRole === 'admin' ? 'admin' : 'user';
      this.errorMessage = `Invalid ${label} credentials.`;
    }
  }
}
