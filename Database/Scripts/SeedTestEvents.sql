/*
	Development-only event seed data.

	Prerequisites:
	- Run the application once so SeedData creates event and interest categories.
	- Register at least one account; the first account is used as the organizer.

	This script removes only prior test events whose names start with
	Event-Future- or Event-Date-Passed-, then recreates 50 test events.
*/

SET NOCOUNT ON;

DECLARE @OrganizerId nvarchar(450) =
(
	SELECT TOP (1) Id
	FROM dbo.AspNetUsers
	ORDER BY Email
);

IF @OrganizerId IS NULL
BEGIN
	THROW 50000, 'Register a user account before running SeedTestEvents.sql.', 1;
END;

DELETE savedEvent
FROM dbo.SavedEvents AS savedEvent
INNER JOIN dbo.Events AS eventRecord
	ON eventRecord.Id = savedEvent.EventId
WHERE eventRecord.Title LIKE 'Event-Future-%'
   OR eventRecord.Title LIKE 'Event-Date-Passed-%';

DELETE eventInterest
FROM dbo.EventInterests AS eventInterest
INNER JOIN dbo.Events AS eventRecord
	ON eventRecord.Id = eventInterest.EventId
WHERE eventRecord.Title LIKE 'Event-Future-%'
   OR eventRecord.Title LIKE 'Event-Date-Passed-%';

DELETE FROM dbo.Events
WHERE Title LIKE 'Event-Future-%'
   OR Title LIKE 'Event-Date-Passed-%';

DECLARE @SeedEvents TABLE
(
	Title nvarchar(200) NOT NULL PRIMARY KEY,
	Description nvarchar(max) NULL,
	EventCategoryName nvarchar(100) NOT NULL,
	InterestCategoryName nvarchar(100) NOT NULL,
	City nvarchar(100) NOT NULL,
	PostalCode nvarchar(20) NULL,
	DayOffset int NOT NULL,
	MinimumPrice decimal(10, 2) NOT NULL,
	ExternalWebsiteUrl nvarchar(2048) NULL
);

;WITH Numbers AS
(
	SELECT 1 AS Number
	UNION ALL
	SELECT Number + 1
	FROM Numbers
	WHERE Number < 50
)
INSERT INTO @SeedEvents
(
	Title,
	Description,
	EventCategoryName,
	InterestCategoryName,
	City,
	PostalCode,
	DayOffset,
	MinimumPrice,
	ExternalWebsiteUrl
)
SELECT
	CASE
		WHEN Number <= 35 THEN CONCAT('Event-Future-', RIGHT(CONCAT('00', Number), 2))
		ELSE CONCAT('Event-Date-Passed-', RIGHT(CONCAT('00', Number - 35), 2))
	END,
	CONCAT('Development test event number ', Number, ' used for event-list filtering.'),
	CASE Number % 7
		WHEN 0 THEN 'Conference'
		WHEN 1 THEN 'Concert'
		WHEN 2 THEN 'Festival'
		WHEN 3 THEN 'Gaming'
		WHEN 4 THEN 'Networking'
		WHEN 5 THEN 'Sports'
		ELSE 'Workshop'
	END,
	CASE Number % 8
		WHEN 0 THEN 'Arts & Culture'
		WHEN 1 THEN 'Business'
		WHEN 2 THEN 'Food & Drink'
		WHEN 3 THEN 'Gaming'
		WHEN 4 THEN 'Health & Fitness'
		WHEN 5 THEN 'Music'
		WHEN 6 THEN 'Sports'
		ELSE 'Technology'
	END,
	CASE Number % 5
		WHEN 0 THEN 'Chicago'
		WHEN 1 THEN 'Milwaukee'
		WHEN 2 THEN 'Madison'
		WHEN 3 THEN 'Detroit'
		ELSE 'Indianapolis'
	END,
	CASE Number % 5
		WHEN 0 THEN '60601'
		WHEN 1 THEN '53202'
		WHEN 2 THEN '53703'
		WHEN 3 THEN '48226'
		ELSE '46204'
	END,
	CASE
		WHEN Number <= 35 THEN Number
		ELSE -(Number - 35)
	END,
	CAST(
		CASE Number % 6
			WHEN 0 THEN 0
			WHEN 1 THEN 10
			WHEN 2 THEN 15
			WHEN 3 THEN 25
			WHEN 4 THEN 50
			ELSE 75
		END
		AS decimal(10, 2)),
	CASE
		WHEN Number % 3 = 0 THEN CONCAT('https://example.com/events/', Number)
		ELSE NULL
	END
FROM Numbers
OPTION (MAXRECURSION 50);

IF EXISTS
(
	SELECT 1
	FROM @SeedEvents AS seedEvent
	WHERE NOT EXISTS
	(
		SELECT 1
		FROM dbo.EventCategories AS category
		WHERE category.Name = seedEvent.EventCategoryName
	)
	OR NOT EXISTS
	(
		SELECT 1
		FROM dbo.InterestCategories AS interest
		WHERE interest.Name = seedEvent.InterestCategoryName
	)
)
BEGIN
	THROW 50001, 'Run the application once to seed event and interest categories, then run this script again.', 1;
END;

INSERT INTO dbo.Events
(
	OrganizerId,
	EventCategoryId,
	Title,
	Description,
	StartsAt,
	EndsAt,
	VenueName,
	City,
	PostalCode,
	MinimumPrice,
	ExternalWebsiteUrl,
	Status,
	CreatedAt
)
SELECT
	@OrganizerId,
	category.Id,
	seedEvent.Title,
	seedEvent.Description,
	DATEADD(DAY, seedEvent.DayOffset, SYSDATETIMEOFFSET()),
	DATEADD(HOUR, 3, DATEADD(DAY, seedEvent.DayOffset, SYSDATETIMEOFFSET())),
	CONCAT('Test Venue ', RIGHT(seedEvent.Title, 2)),
	seedEvent.City,
	seedEvent.PostalCode,
	seedEvent.MinimumPrice,
	seedEvent.ExternalWebsiteUrl,
	2,
	SYSDATETIMEOFFSET()
FROM @SeedEvents AS seedEvent
INNER JOIN dbo.EventCategories AS category
	ON category.Name = seedEvent.EventCategoryName;

INSERT INTO dbo.EventInterests
(
	EventId,
	InterestCategoryId
)
SELECT
	eventRecord.Id,
	interest.Id
FROM dbo.Events AS eventRecord
INNER JOIN @SeedEvents AS seedEvent
	ON seedEvent.Title = eventRecord.Title
INNER JOIN dbo.InterestCategories AS interest
	ON interest.Name = seedEvent.InterestCategoryName;

SELECT
	eventRecord.Title,
	eventRecord.StartsAt,
	eventRecord.City,
	eventRecord.MinimumPrice,
	eventRecord.Status,
	interest.Name AS Interest
FROM dbo.Events AS eventRecord
INNER JOIN dbo.EventInterests AS eventInterest
	ON eventInterest.EventId = eventRecord.Id
INNER JOIN dbo.InterestCategories AS interest
	ON interest.Id = eventInterest.InterestCategoryId
WHERE eventRecord.Title LIKE 'Event-Future-%'
   OR eventRecord.Title LIKE 'Event-Date-Passed-%'
ORDER BY eventRecord.StartsAt;
