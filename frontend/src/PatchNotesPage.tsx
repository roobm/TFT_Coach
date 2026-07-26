const PATCH_NOTES_HUB_URL =
  'https://teamfighttactics.leagueoflegends.com/en-us/news/game-updates/';

const OFFICIAL_LINKS = [
  {
    label: 'TFT Game Updates (Patch Notes Hub)',
    description: 'Official patch notes, B-patches, and gameplay changes.',
    href: PATCH_NOTES_HUB_URL,
  },
  {
    label: 'TFT Dev Updates',
    description: 'Developer insights on set goals, systems, and balancing direction.',
    href: 'https://teamfighttactics.leagueoflegends.com/en-us/news/dev/',
  },
  {
    label: 'TFT News Homepage',
    description: 'All TFT announcements in one place (events, updates, and features).',
    href: 'https://teamfighttactics.leagueoflegends.com/en-us/news/',
  },
  {
    label: 'Riot Service Status (TFT)',
    description: 'Live service incidents and maintenance affecting TFT.',
    href: 'https://status.riotgames.com/?locale=en_US&product=tft',
  },
];

export function PatchNotesPage() {
  return (
    <section className="patch-notes-page">
      <article className="patch-notes-panel">
        <p className="history-kicker">Meta tracking</p>
        <h2 className="patch-notes-title">Patch Notes</h2>
        <p className="patch-notes-description">
          Keep your coaching context aligned with live TFT balance changes. Open the official updates
          hub for the newest patch details and follow-up tweaks.
        </p>

        <a
          href={PATCH_NOTES_HUB_URL}
          className="patch-notes-cta"
          target="_blank"
          rel="noopener noreferrer"
        >
          Open official TFT patch notes
        </a>
      </article>

      <article className="patch-notes-panel">
        <h3 className="patch-notes-links-title">Useful official TFT update links</h3>
        <ul className="patch-notes-links">
          {OFFICIAL_LINKS.map((link) => (
            <li key={link.href} className="patch-notes-link-item">
              <a href={link.href} target="_blank" rel="noopener noreferrer">
                {link.label}
              </a>
              <p>{link.description}</p>
            </li>
          ))}
        </ul>
      </article>
    </section>
  );
}
