import { NgModule } from '@angular/core';
import { BrowserModule } from '@angular/platform-browser';

import { AppRoutingModule } from './app-routing.module';
import { AppComponent } from './app.component';

import { LoginComponent } from './components/login/login.component';
import { UserComponent } from './components/user/user.component';
import { AdminComponent } from './components/admin/admin.component';
import { HealthStatusComponent } from './components/health-status/health-status.component';
import { LandingComponent } from './components/landing/landing.component';
import { FormsModule } from '@angular/forms';
import { HttpClientModule } from '@angular/common/http';
import { QnAComponent } from './components/qn-a/qn-a.component';

@NgModule({
  declarations: [
    AppComponent,
    AdminComponent,
    HealthStatusComponent,
    LandingComponent,
    LoginComponent,
    UserComponent,
    QnAComponent
  ],
  imports: [
    FormsModule,
    BrowserModule,
    HttpClientModule,
    AppRoutingModule
  ],
  providers: [],
  bootstrap: [AppComponent]
})
export class AppModule { }
