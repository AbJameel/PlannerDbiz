import { useEffect, useState } from 'react';
import { api } from '../api/client';
import type { MailboxItem } from '../types';

export function MailboxPage() {
  const [items, setItems] = useState<MailboxItem[]>([]);

  useEffect(() => {
    api.getMailbox().then((data) => setItems(data as MailboxItem[]));
  }, []);

  return (
    <section className="panel">
      <div className="panel-header">
        <div>
          <p className="eyebrow">Mailbox</p>
          <h2>Mock inbox</h2>
        </div>
      </div>
      <div className="stack-list">
        {items.map((item) => (
          <div key={item.id} className="task-item mailbox-item">
            <div>
              <div className="task-title-row">
                <strong>{item.subject}</strong>
                {!item.isRead && <span className="pill pill-high">New</span>}
              </div>
              <p>{item.fromEmail}</p>
              <small>{item.snippet}</small>
            </div>
            <div className="mail-meta">{new Date(item.receivedOn).toLocaleString()}</div>
          </div>
        ))}
      </div>
    </section>
  );
}
