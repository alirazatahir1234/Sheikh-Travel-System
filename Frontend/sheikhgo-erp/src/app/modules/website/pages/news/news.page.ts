import { Component, OnInit, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { CtaBandComponent } from '../../shared/cta-band.component';
import { WebsiteSeoService } from '../../core/seo.service';
import { NEWS_ANNOUNCEMENTS } from '../../core/news-items';
import { WebsiteTranslatePipe } from '../../shared/website-translate.pipe';

@Component({
  selector: 'app-news-page',
  standalone: true,
  imports: [RouterLink, MatIconModule, CtaBandComponent, WebsiteTranslatePipe],
  templateUrl: './news.page.html',
  styleUrl: './news.page.scss',
})
export class NewsPage implements OnInit {
  private readonly seo = inject(WebsiteSeoService);
  readonly items = NEWS_ANNOUNCEMENTS;

  ngOnInit(): void {
    this.seo.set(
      'News & announcements',
      'Product updates, new Nexus features, and company news from SheikhGo Technologies.',
      '/news',
    );
  }
}
