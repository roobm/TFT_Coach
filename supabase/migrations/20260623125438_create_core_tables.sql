-- Core schema for TFT Coach (see plan Section 4).

-- Patch and champion knowledge (vector search).
create table tft_patch_data (
    id          serial primary key,
    patch       varchar(10),
    category    varchar(50),   -- 'champion' | 'item' | 'augment' | 'trait' | 'patch_note'
    content     text,
    embedding   vector(1536),
    created_at  timestamptz default now()
);

-- Personal match history (structured stats).
create table match_history (
    id          serial primary key,
    user_id     uuid references auth.users(id),
    played_at   timestamptz,
    patch       varchar(10),
    placement   integer,
    comp        varchar(100),
    augments    text[],
    gold_left   integer,
    final_hp    integer,
    rounds_lost integer
);

-- Round-level detail (vector search + stats).
create table round_logs (
    id          serial primary key,
    match_id    integer references match_history(id),
    round       varchar(10),
    content     text,
    embedding   vector(1536),
    created_at  timestamptz default now()
);

-- User profiles and payment.
create table profiles (
    id          uuid references auth.users(id) primary key,
    is_paid     boolean default false,
    created_at  timestamptz default now()
);

-- Approximate nearest-neighbour indexes for cosine similarity search.
create index tft_patch_data_embedding_idx
    on tft_patch_data using ivfflat (embedding vector_cosine_ops) with (lists = 100);

create index round_logs_embedding_idx
    on round_logs using ivfflat (embedding vector_cosine_ops) with (lists = 100);
