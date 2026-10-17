import { ChangeDetectionStrategy, Component } from '@angular/core';
import { Card } from '../../shared/components/card/card';

@Component({
  imports: [Card],
  templateUrl: './users.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UsersPage {}
