import { Component, EventEmitter, Input, Output } from '@angular/core';
import { ArticleLink } from '../ui.types';

@Component({
  selector: 'stb-article-links-card',
  templateUrl: './article-links-card.component.html',
  styleUrls: ['./article-links-card.component.scss']
})
export class ArticleLinksCardComponent {
  @Input() title = 'Popular Articles';
  @Input() icon = 'article';
  @Input() articles: ArticleLink[] = [];
  @Input() emptyMessage = 'No articles yet.';

  @Output() open = new EventEmitter<ArticleLink>();

  trackById(_i: number, a: ArticleLink) { return a.id; }
}
