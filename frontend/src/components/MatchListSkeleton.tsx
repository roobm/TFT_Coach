export function MatchListSkeleton({ count = 4 }: { count?: number }) {
  return (
    <ul className="match-list" aria-hidden="true">
      {Array.from({ length: count }).map((_, i) => (
        <li key={i} className="skeleton-card">
          <div className="sk-row">
            <div className="sk sk-badge" />
            <div style={{ flex: 1 }}>
              <div className="sk sk-line" style={{ width: '40%', marginBottom: 8 }} />
              <div className="sk sk-line" style={{ width: '25%' }} />
            </div>
          </div>
          <div className="sk sk-stats" />
          <div className="sk sk-chips" style={{ marginBottom: 8 }} />
          <div className="sk sk-chips" style={{ width: '70%' }} />
        </li>
      ))}
    </ul>
  );
}
