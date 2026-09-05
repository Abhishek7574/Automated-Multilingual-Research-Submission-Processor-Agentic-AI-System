import { Component } from '@angular/core';
import { Router } from '@angular/router';

interface Feature {
  icon: string;
  title: string;
  description: string;
}

@Component({
  selector: 'app-landing',
  templateUrl: './landing.component.html',
  styleUrls: ['./landing.component.css']
})
export class LandingComponent {

  features: Feature[] = [
    {
      icon: '🧠',
      title: 'Smart Extraction',
      description:
        'Automatically extract key metadata, abstracts, and structured data from research papers across multiple formats.',
    },
    {
      icon: '🛡️',
      title: 'Content Safety',
      description:
        'AI-powered content moderation ensures submissions meet ethical guidelines and community standards.',
    },
    {
      icon: '🔍',
      title: 'Plagiarism Detection',
      description:
        'Advanced similarity analysis cross-references submissions against global academic databases in real-time.',
    },
    {
      icon: '🌐',
      title: 'Language Detection',
      description:
        'Automatic identification and handling of 100+ languages enabling truly multilingual research workflows.',
    },
    {
      icon: '💬',
      title: 'Q&A System',
      description:
        'Interact with research content through a natural language Q&A interface powered by AI.',
    },
    {
      icon: '📋',
      title: 'Admin Reviewer',
      description:
        'Full administrative dashboard to manage, annotate, approve or reject submissions with audit trails.',
    },
  ];

  constructor(private router: Router) { }

  goToLogin(role: 'user' | 'admin'): void {
    this.router.navigate(['/login'], { queryParams: { role } });
  }
}
