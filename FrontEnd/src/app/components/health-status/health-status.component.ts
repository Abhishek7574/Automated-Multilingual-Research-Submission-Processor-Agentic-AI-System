import { Component, OnInit } from '@angular/core';
import { HealthResponse } from '../../interfaces/interfaces';
import { ApiService } from '../../services/api.services';

type StatusState = 'checking' | 'healthy' | 'unreachable';

@Component({
  selector: 'app-health-status',
  templateUrl: './health-status.component.html',
  styleUrls: ['./health-status.component.css']
})
export class HealthStatusComponent implements OnInit {

  state: StatusState = 'checking';
  version: string = '';

  constructor(private api: ApiService) { }

  ngOnInit(): void {
    this.checkHealth();
  }

  checkHealth(): void {
    this.state = 'checking';

    this.api.getHealth().subscribe({
      next: (res: HealthResponse) => {
        this.version = res.version;
        this.state = 'healthy';
      },
      error: () => {
        this.state = 'unreachable';
      }
    });
  }
}
