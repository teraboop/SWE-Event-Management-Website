/*
	Demo seed data for presentations. Development databases only.

	Prerequisites:
	- The database is migrated up to AddCartAndOrderItems.
	- The application has run once, so SeedData created the roles and the
	  event and interest categories.
	- organizer@test.com and vendor@test.com are registered.

	What it does (all in one transaction; any error rolls everything back):
	1. Removes earlier seed data so the script can be re-run:
	   - every event titled Event-Future-XX or Event-Date-Passed-XX, plus
	     its tickets, reviews, announcements, panels, vendor applications,
	     saved events and cart lines;
	   - the demo attendee accounts (*.demo@test.com) and everything they own;
	   - vendor@test.com's products whose names match the list below.
	   Orders that lose lines get their totals recalculated, and orders left
	   empty are deleted.
	2. Creates six demo attendees. They all sign in with the password DemoPass123:
	   alex.demo, jordan.demo, sam.demo, taylor.demo, morgan.demo, casey.demo (@test.com).
	3. Creates 35 upcoming and 15 past events, shared between
	   organizer@test.com and vendor@test.com, each with ticket types.
	   Both accounts get the Organizer role, and a display name if they don't
	   have one yet.
	4. Ticket orders: attendees, and each test account at the other's events.
	   Event-Future-02's VIP tier is sold out; Event-Future-03 has 3 VIP left.
	5. Reviews of past events from the ticket holders.
	6. Announcements, approved and pending panels, vendors approved at
	   events, and pending vendor, panel and event applications.
	7. A vendor profile for vendor@test.com, approved for online sales, with
	   30 products and some merchandise orders.

	Dates are relative to when the script runs, so re-run it before a demo
	to keep "upcoming" events upcoming. Event times are Central (UTC-6).
	Accounts whose roles changed must sign out and back in.
*/

-- Filtered unique indexes need these on. SSMS sets them by default; sqlcmd does not.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Now datetimeoffset(7) = SYSDATETIMEOFFSET();
DECLARE @Today date = CAST(SWITCHOFFSET(@Now, '-06:00') AS date);

DECLARE @OrganizerId nvarchar(450) = (SELECT Id FROM dbo.AspNetUsers WHERE NormalizedEmail = 'ORGANIZER@TEST.COM');
DECLARE @VendorId nvarchar(450) = (SELECT Id FROM dbo.AspNetUsers WHERE NormalizedEmail = 'VENDOR@TEST.COM');

IF @OrganizerId IS NULL OR @VendorId IS NULL
BEGIN
	THROW 50000, 'Register organizer@test.com and vendor@test.com before running SeedDemoData.sql.', 1;
END;

IF (SELECT COUNT(*) FROM dbo.EventCategories) < 7 OR (SELECT COUNT(*) FROM dbo.InterestCategories) < 8
BEGIN
	THROW 50001, 'Run the application once so it seeds event and interest categories, then run this script again.', 1;
END;

DECLARE @OrganizerName nvarchar(256) = (SELECT UserName FROM dbo.AspNetUsers WHERE Id = @OrganizerId);
DECLARE @VendorName nvarchar(256) = (SELECT UserName FROM dbo.AspNetUsers WHERE Id = @VendorId);

/* ------------------------------------------------------------------------
   Reference data used by both the cleanup and the inserts
   ------------------------------------------------------------------------ */

DECLARE @Numbers TABLE (N int NOT NULL PRIMARY KEY);
;WITH Numbers AS
(
	SELECT 1 AS N
	UNION ALL
	SELECT N + 1 FROM Numbers WHERE N < 60
)
INSERT INTO @Numbers (N)
SELECT N FROM Numbers
OPTION (MAXRECURSION 60);

DECLARE @SeedProducts TABLE
(
	SortOrder int NOT NULL PRIMARY KEY,
	Name nvarchar(150) NOT NULL UNIQUE,
	Description nvarchar(2000) NOT NULL,
	Price decimal(10, 2) NOT NULL,
	QuantityInStock int NOT NULL,
	IsListed bit NOT NULL
);

INSERT INTO @SeedProducts (SortOrder, Name, Description, Price, QuantityInStock, IsListed)
VALUES
	(1, 'Retro Console Tee - Black', 'Soft cotton tee with a distressed print of a classic home console. Unisex sizes S to XXL.', 24.99, 40, 1),
	(2, 'Pixel Heart Enamel Pin', 'Hard enamel pin shaped like an 8-bit heart, with a rubber clutch back. About 1 inch wide.', 8.00, 120, 1),
	(3, '8-Bit Controller Keychain', 'Metal keychain shaped like an old-school gamepad, with clicky (non-working) buttons.', 6.50, 85, 1),
	(4, 'Cartridge Cleaning Kit', 'A novelty kit with swabs and a cloth for keeping your old cartridges looking their best.', 12.00, 30, 1),
	(5, 'Arcade Token Necklace', 'A real brass arcade token on an 18-inch stainless chain.', 18.00, 25, 1),
	(6, 'Synthwave Sunset Poster 18x24', 'Matte art print of a neon grid sunset. Ships rolled in a tube, frame not included.', 20.00, 50, 1),
	(7, 'VHS Rewind Mug', '12 oz ceramic mug printed with a reminder to be kind and rewind. Dishwasher safe.', 14.00, 60, 1),
	(8, 'Cassette Tape Coaster Set (4)', 'Four silicone coasters shaped like mixtapes, in four colors.', 16.00, 35, 1),
	(9, 'Pixel Cloud Desk Lamp', 'USB-powered LED lamp shaped like a pixel-art cloud, with three brightness levels.', 39.99, 12, 1),
	(10, 'Retro Gamer Snapback', 'Embroidered snapback cap with a pixel joystick logo. One size fits most.', 22.00, 28, 1),
	(11, 'Vinyl Record Wall Clock', 'A wall clock cut from a real recycled vinyl record. Each one is slightly different.', 34.00, 9, 1),
	(12, 'Floppy Disk Notebook', 'A5 dotted notebook with a cover designed like a 3.5-inch floppy disk. 120 pages.', 9.50, 70, 1),
	(13, 'Mixtape Sticker Pack (20)', 'Twenty weatherproof vinyl stickers of tapes, consoles and boomboxes.', 7.00, 150, 1),
	(14, 'Mini Neon Arcade Sign', 'LED neon-style sign that reads INSERT COIN. USB powered, about 12 inches wide.', 55.00, 4, 1),
	(15, 'Classic Handheld Silicone Case', 'Protective silicone sleeve for brick-style handheld consoles, in translucent purple.', 11.00, 0, 1),
	(16, 'Boombox Bluetooth Speaker', 'A palm-sized boombox replica with a real Bluetooth speaker inside. About 10 hours of battery.', 69.00, 6, 1),
	(17, 'Retro Racer Dice Set', 'Seven polyhedral dice in a neon racing-stripe resin, with a pouch.', 13.00, 40, 1),
	(18, 'CRT TV Plush', 'A squishy plush shaped like a chunky CRT television with a smiling face.', 19.00, 3, 1),
	(19, 'Game Night Trivia Cards: The 90s', '200 trivia questions about 90s music, movies, toys and games.', 15.00, 45, 1),
	(20, 'Pixel Art Canvas Print', '16x16 inch canvas of a pixel-art city skyline, ready to hang.', 45.00, 8, 1),
	(21, 'Rainbow Joystick Pen', 'Gel pen with a working mini joystick topper. Black ink.', 4.50, 200, 1),
	(22, 'Cassette Player Enamel Pin', 'Hard enamel pin of a portable tape player with tiny headphones.', 8.00, 0, 1),
	(23, 'Retro Windbreaker Jacket', 'Color-block windbreaker in teal, purple and pink, with a zip pocket.', 59.00, 15, 1),
	(24, 'Glow-in-the-Dark Space Arcade Tee', 'Black tee with a glow-in-the-dark print of a classic space shooter.', 26.00, 22, 1),
	(25, 'Cartridge Display Shelf', 'Wall shelf that holds 12 game cartridges upright. Hardware included.', 49.00, 5, 1),
	(26, 'Arcade Button Coaster', 'A cork-backed coaster shaped like a big red arcade button.', 10.00, 55, 1),
	(27, 'Instant-Photo Magnets (6)', 'Six fridge magnets styled like instant photos, ready for your own pictures.', 12.50, 33, 1),
	(28, '80s Neon Socks (3 pairs)', 'Three pairs of crew socks in neon geometric patterns.', 17.00, 48, 1),
	(29, 'Vintage Controller Wall Art', 'Framed 11x14 print showing the evolution of game controllers.', 38.00, 10, 1),
	(30, 'Mystery Retro Box', 'A surprise box of five retro collectibles. Still a draft, so it is hidden from the shop.', 35.00, 20, 0);

/* ------------------------------------------------------------------------
   1. Clean up earlier seed data
   ------------------------------------------------------------------------ */

BEGIN TRY
	BEGIN TRANSACTION;

	DECLARE @AffectedOrders TABLE (OrderId int NOT NULL);

	-- Demo attendees. Deleting the accounts cascades to their orders,
	-- tickets, reviews, saved events, carts, applications and vendor profile.
	DELETE FROM dbo.AspNetUsers
	WHERE NormalizedEmail LIKE '%.DEMO@TEST.COM';

	-- Earlier seed events and what hangs off them.
	DECLARE @OldEvents TABLE (Id int NOT NULL PRIMARY KEY, BackgroundImageId int NULL);

	INSERT INTO @OldEvents (Id, BackgroundImageId)
	SELECT Id, BackgroundImageId
	FROM dbo.Events
	WHERE Title LIKE 'Event-Future-%'
	   OR Title LIKE 'Event-Date-Passed-%';

	-- Tickets block deleting ticket types, so they go first.
	DELETE ticket
	OUTPUT deleted.OrderId INTO @AffectedOrders (OrderId)
	FROM dbo.Tickets AS ticket
	INNER JOIN dbo.EventTicketTypes AS ticketType ON ticketType.Id = ticket.EventTicketTypeId
	INNER JOIN @OldEvents AS oldEvent ON oldEvent.Id = ticketType.EventId;

	-- An approved event application points at the event it created.
	DELETE application
	FROM dbo.EventApplications AS application
	INNER JOIN @OldEvents AS oldEvent ON oldEvent.Id = application.CreatedEventId;

	-- Cascades to ticket types, cart lines, interests, saved events,
	-- reviews, announcements, and vendor and panel applications.
	DELETE eventRecord
	FROM dbo.Events AS eventRecord
	INNER JOIN @OldEvents AS oldEvent ON oldEvent.Id = eventRecord.Id;

	DELETE eventImage
	FROM dbo.EventImages AS eventImage
	INNER JOIN @OldEvents AS oldEvent ON oldEvent.BackgroundImageId = eventImage.Id
	WHERE NOT EXISTS (SELECT 1 FROM dbo.Events WHERE BackgroundImageId = eventImage.Id)
	  AND NOT EXISTS (SELECT 1 FROM dbo.EventApplications WHERE BackgroundImageId = eventImage.Id)
	  AND NOT EXISTS (SELECT 1 FROM dbo.VendorProducts WHERE ImageId = eventImage.Id);

	-- vendor@test.com's seeded products, matched by name.
	DECLARE @OldProducts TABLE (Id int NOT NULL PRIMARY KEY);

	INSERT INTO @OldProducts (Id)
	SELECT product.Id
	FROM dbo.VendorProducts AS product
	INNER JOIN dbo.VendorProfiles AS profile ON profile.Id = product.VendorProfileId
	WHERE profile.OwnerUserId = @VendorId
	  AND product.Name IN (SELECT Name FROM @SeedProducts);

	DELETE cartItem
	FROM dbo.CartItems AS cartItem
	INNER JOIN @OldProducts AS oldProduct ON oldProduct.Id = cartItem.VendorProductId;

	DELETE orderItem
	OUTPUT deleted.OrderId INTO @AffectedOrders (OrderId)
	FROM dbo.OrderItems AS orderItem
	INNER JOIN @OldProducts AS oldProduct ON oldProduct.Id = orderItem.VendorProductId;

	DELETE product
	FROM dbo.VendorProducts AS product
	INNER JOIN @OldProducts AS oldProduct ON oldProduct.Id = product.Id;

	-- Orders that lost every line are removed; the rest get correct totals.
	DELETE orderRecord
	FROM dbo.Orders AS orderRecord
	WHERE orderRecord.Id IN (SELECT OrderId FROM @AffectedOrders)
	  AND NOT EXISTS (SELECT 1 FROM dbo.Tickets WHERE OrderId = orderRecord.Id)
	  AND NOT EXISTS (SELECT 1 FROM dbo.OrderItems WHERE OrderId = orderRecord.Id);

	UPDATE orderRecord
	SET TotalAmount =
		ISNULL((SELECT SUM(UnitPrice) FROM dbo.Tickets WHERE OrderId = orderRecord.Id), 0) +
		ISNULL((SELECT SUM(UnitPrice * Quantity) FROM dbo.OrderItems WHERE OrderId = orderRecord.Id), 0)
	FROM dbo.Orders AS orderRecord
	WHERE orderRecord.Id IN (SELECT OrderId FROM @AffectedOrders);

	/* --------------------------------------------------------------------
	   2. Accounts
	   -------------------------------------------------------------------- */

	DECLARE @DemoUsers TABLE
	(
		Idx int NOT NULL PRIMARY KEY,
		Id nvarchar(450) NOT NULL,
		Email nvarchar(256) NOT NULL,
		DisplayName nvarchar(100) NOT NULL,
		City nvarchar(100) NOT NULL,
		PostalCode nvarchar(20) NOT NULL,
		PasswordHash nvarchar(max) NOT NULL,
		SecurityStamp nvarchar(100) NOT NULL
	);

	-- Password for all six: DemoPass123 (ASP.NET Identity v3 hashes).
	INSERT INTO @DemoUsers (Idx, Id, Email, DisplayName, City, PostalCode, PasswordHash, SecurityStamp)
	VALUES
		(1, '532c9d88-fa45-4b4f-adfd-973f0280f262', 'alex.demo@test.com', 'Alex R.', 'Chicago', '60601', 'AQAAAAIAAYagAAAAEKbpVG4mAtyF7tvDA44MEq/L5r1A7WqW03eHtyt29ylButU+WFn4dOtbP6l+T0R9FA==', '61049E1A14BA4FB68C1821EF9753D521'),
		(2, '93e8e696-3cb4-4e42-91c8-45a0ba004a41', 'jordan.demo@test.com', 'Jordan L.', 'Milwaukee', '53202', 'AQAAAAIAAYagAAAAECexEXKwtI4l0ZHCF969f5F8RK6sM0ygAmsVo+WCNOoFFqvqcegfzaE4Z2YA53yOdA==', 'E1F413BF0C4645F898F168EEAFEE2243'),
		(3, '89057e83-79c2-4944-a64a-a3b1707473fd', 'sam.demo@test.com', 'Sam P.', 'Madison', '53703', 'AQAAAAIAAYagAAAAELyJWXvvjHxeiqJ2PMDh8gj0e6oYdKTqwmdICmUWKHywqEShj0cPwWe2jrqK2/X16g==', '06566FBC13464A219B75A839594767B4'),
		(4, 'f7b6bf10-37cb-4e64-bab7-a1c5bcef6887', 'taylor.demo@test.com', 'Taylor B.', 'Detroit', '48226', 'AQAAAAIAAYagAAAAECJ6Dwi7ZYs7t9/z2ZU0j0KoQ5jUoxNWQhT1Hel4yBC2VQgliYpV+f7hbcG+WhypSw==', '09E2015E261149ECBA414DEDF23B38FC'),
		(5, 'f608979b-c755-4f5f-984c-f9337089a4bd', 'morgan.demo@test.com', 'Morgan C.', 'Indianapolis', '46204', 'AQAAAAIAAYagAAAAEBe1nHdcjrPQ8kDt3eJIJkYEEgHZiGcY302gLNanVYyoxkdHz/xDbVuP2BszRSLlAg==', '41B6A1F7119D469CAC839753A14EAAC2'),
		(6, 'fece72c8-7033-4184-a40d-3e323319ee75', 'casey.demo@test.com', 'Casey N.', 'Chicago', '60601', 'AQAAAAIAAYagAAAAELAyZIPiQ7dVa0alnM7N1bwNP5THoDdWiG0/ce80Nd6lz5HP5Kwg3PkNzR6zkSlEOA==', 'E22FA730A3D6405DBD9B5BD3893DC5A6');

	INSERT INTO dbo.AspNetUsers
	(
		Id, UserName, NormalizedUserName, Email, NormalizedEmail, EmailConfirmed,
		PasswordHash, SecurityStamp, ConcurrencyStamp, PhoneNumberConfirmed,
		TwoFactorEnabled, LockoutEnabled, AccessFailedCount, DisplayName, City, PostalCode
	)
	SELECT
		Id, Email, UPPER(Email), Email, UPPER(Email), 1,
		PasswordHash, SecurityStamp, CONVERT(nvarchar(36), NEWID()), 0,
		0, 1, 0, DisplayName, City, PostalCode
	FROM @DemoUsers;

	INSERT INTO dbo.AspNetUserRoles (UserId, RoleId)
	SELECT demoUser.Id, role.Id
	FROM @DemoUsers AS demoUser
	CROSS JOIN dbo.AspNetRoles AS role
	WHERE role.NormalizedName = 'ATTENDEE';

	-- Both test accounts run events, so both need the Organizer role.
	INSERT INTO dbo.AspNetUserRoles (UserId, RoleId)
	SELECT account.UserId, role.Id
	FROM (VALUES (@OrganizerId), (@VendorId)) AS account (UserId)
	CROSS JOIN dbo.AspNetRoles AS role
	WHERE role.NormalizedName = 'ORGANIZER'
	  AND NOT EXISTS
	  (
		SELECT 1 FROM dbo.AspNetUserRoles AS existing
		WHERE existing.UserId = account.UserId AND existing.RoleId = role.Id
	  );

	-- Reviews show display names, so give the test accounts one if they have none.
	UPDATE dbo.AspNetUsers SET DisplayName = 'Demo Organizer' WHERE Id = @OrganizerId AND DisplayName IS NULL;
	UPDATE dbo.AspNetUsers SET DisplayName = 'Demo Vendor' WHERE Id = @VendorId AND DisplayName IS NULL;

	-- Everyone who buys tickets in this script. 7 and 8 are the test accounts.
	DECLARE @Buyers TABLE (Idx int NOT NULL PRIMARY KEY, UserId nvarchar(450) NOT NULL, Email nvarchar(256) NOT NULL);

	INSERT INTO @Buyers (Idx, UserId, Email)
	SELECT Idx, Id, Email FROM @DemoUsers
	UNION ALL SELECT 7, @OrganizerId, 'organizer@test.com'
	UNION ALL SELECT 8, @VendorId, 'vendor@test.com';

	INSERT INTO dbo.UserInterests (UserId, InterestCategoryId)
	SELECT demoUser.Id, interest.Id
	FROM @DemoUsers AS demoUser
	CROSS APPLY (VALUES
		(CASE demoUser.Idx WHEN 1 THEN 'Gaming' WHEN 2 THEN 'Music' WHEN 3 THEN 'Technology' WHEN 4 THEN 'Sports' WHEN 5 THEN 'Arts & Culture' ELSE 'Food & Drink' END),
		(CASE demoUser.Idx WHEN 1 THEN 'Technology' WHEN 2 THEN 'Arts & Culture' WHEN 3 THEN 'Gaming' WHEN 4 THEN 'Health & Fitness' WHEN 5 THEN 'Business' ELSE 'Music' END)
	) AS picked (Name)
	INNER JOIN dbo.InterestCategories AS interest ON interest.Name = picked.Name;

	/* --------------------------------------------------------------------
	   3. Events
	   -------------------------------------------------------------------- */

	DECLARE @SeedEvents TABLE
	(
		Number int NOT NULL PRIMARY KEY,
		Title nvarchar(200) NOT NULL UNIQUE,
		IsPast bit NOT NULL,
		OwnerId nvarchar(450) NOT NULL,
		OwnerName nvarchar(256) NOT NULL,
		EventCategoryName nvarchar(100) NOT NULL,
		InterestName nvarchar(100) NOT NULL,
		SecondInterestName nvarchar(100) NOT NULL,
		City nvarchar(100) NOT NULL,
		PostalCode nvarchar(20) NOT NULL,
		VenueName nvarchar(200) NOT NULL,
		StartsAt datetimeoffset(7) NOT NULL,
		EndsAt datetimeoffset(7) NOT NULL,
		BasePrice decimal(10, 2) NOT NULL,
		TicketSalesMode int NOT NULL,
		ExternalTicketingUrl nvarchar(2048) NULL,
		ExternalWebsiteUrl nvarchar(2048) NULL,
		Description nvarchar(4000) NOT NULL,
		EventId int NULL
	);

	-- 1-35 are upcoming (1 is tomorrow), 36-50 are past (36 was yesterday).
	-- Every third event belongs to vendor@test.com, the rest to organizer@test.com.
	INSERT INTO @SeedEvents
	(
		Number, Title, IsPast, OwnerId, OwnerName, EventCategoryName, InterestName, SecondInterestName,
		City, PostalCode, VenueName, StartsAt, EndsAt, BasePrice, TicketSalesMode,
		ExternalTicketingUrl, ExternalWebsiteUrl, Description
	)
	SELECT
		n.N,
		CASE WHEN n.N <= 35 THEN CONCAT('Event-Future-', RIGHT(CONCAT('00', n.N), 2))
		     ELSE CONCAT('Event-Date-Passed-', RIGHT(CONCAT('00', n.N - 35), 2)) END,
		CASE WHEN n.N <= 35 THEN 0 ELSE 1 END,
		CASE WHEN n.N % 3 = 0 THEN @VendorId ELSE @OrganizerId END,
		CASE WHEN n.N % 3 = 0 THEN @VendorName ELSE @OrganizerName END,
		category.Name,
		CASE n.N % 8 WHEN 0 THEN 'Arts & Culture' WHEN 1 THEN 'Business' WHEN 2 THEN 'Food & Drink' WHEN 3 THEN 'Gaming'
		             WHEN 4 THEN 'Health & Fitness' WHEN 5 THEN 'Music' WHEN 6 THEN 'Sports' ELSE 'Technology' END,
		CASE (n.N + 3) % 8 WHEN 0 THEN 'Arts & Culture' WHEN 1 THEN 'Business' WHEN 2 THEN 'Food & Drink' WHEN 3 THEN 'Gaming'
		                   WHEN 4 THEN 'Health & Fitness' WHEN 5 THEN 'Music' WHEN 6 THEN 'Sports' ELSE 'Technology' END,
		place.City,
		place.PostalCode,
		CONCAT(place.Venue, CASE n.N % 3 WHEN 0 THEN ' - Hall A' WHEN 1 THEN ' - Ballroom' ELSE ' - Main Stage' END),
		timing.StartsAt,
		DATEADD(HOUR, CASE n.N % 3 WHEN 0 THEN 3 WHEN 1 THEN 5 ELSE 8 END, timing.StartsAt),
		price.BasePrice,
		CASE WHEN n.N % 10 = 7 THEN 2                                 -- tickets sold on another site
		     WHEN price.BasePrice = 0 AND n.N % 4 = 0 THEN 3          -- free, no ticket needed
		     ELSE 1 END,                                              -- sold here
		CASE WHEN n.N % 10 = 7 THEN CONCAT('https://example.com/tickets/', n.N) END,
		CASE WHEN n.N % 3 = 0 THEN CONCAT('https://example.com/events/', n.N) END,
		CONCAT(
			CASE category.Name
				WHEN 'Conference' THEN 'A day of talks and hands-on sessions about the games, gadgets and pop culture we grew up with.'
				WHEN 'Concert' THEN 'Live music from local bands covering the hits of the 80s and 90s, with a light show to match.'
				WHEN 'Festival' THEN 'Food trucks, arcade cabinets, vendors and live entertainment for all ages.'
				WHEN 'Gaming' THEN 'Free-play arcade cabinets and classic consoles, plus a high-score tournament with prizes.'
				WHEN 'Networking' THEN 'Meet fellow collectors, creators and fans over snacks and retro tunes.'
				WHEN 'Sports' THEN 'A retro-themed fun run and field games. Costumes from your favorite decade are encouraged.'
				ELSE 'A hands-on workshop led by local makers. All materials are included with your ticket.'
			END,
			' Hosted in ', place.City, '.')
	FROM @Numbers AS n
	CROSS APPLY (SELECT CASE n.N % 7 WHEN 0 THEN 'Conference' WHEN 1 THEN 'Concert' WHEN 2 THEN 'Festival' WHEN 3 THEN 'Gaming'
	                                 WHEN 4 THEN 'Networking' WHEN 5 THEN 'Sports' ELSE 'Workshop' END AS Name) AS category
	CROSS APPLY (SELECT
		CASE n.N % 5 WHEN 0 THEN 'Chicago' WHEN 1 THEN 'Milwaukee' WHEN 2 THEN 'Madison' WHEN 3 THEN 'Detroit' ELSE 'Indianapolis' END AS City,
		CASE n.N % 5 WHEN 0 THEN '60601' WHEN 1 THEN '53202' WHEN 2 THEN '53703' WHEN 3 THEN '48226' ELSE '46204' END AS PostalCode,
		CASE n.N % 5 WHEN 0 THEN 'Lakeside Expo Center' WHEN 1 THEN 'Riverwalk Convention Hall' WHEN 2 THEN 'Capitol Square Pavilion'
		             WHEN 3 THEN 'Motor City Event Center' ELSE 'Circle City Arena' END AS Venue) AS place
	CROSS APPLY (SELECT TODATETIMEOFFSET(
		DATEADD(HOUR,
			CASE n.N % 4 WHEN 0 THEN 10 WHEN 1 THEN 12 WHEN 2 THEN 18 ELSE 19 END,
			CAST(DATEADD(DAY, CASE WHEN n.N <= 35 THEN n.N ELSE -(n.N - 35) END, @Today) AS datetime2(7))),
		'-06:00') AS StartsAt) AS timing
	CROSS APPLY (SELECT CAST(CASE n.N % 6 WHEN 0 THEN 0 WHEN 1 THEN 10 WHEN 2 THEN 15 WHEN 3 THEN 25 WHEN 4 THEN 50 ELSE 75 END
	                         AS decimal(10, 2)) AS BasePrice) AS price
	WHERE n.N <= 50;

	-- Open vendor applications and panel proposals on some events.
	INSERT INTO dbo.Events
	(
		OrganizerId, EventCategoryId, Title, Description, StartsAt, EndsAt, VenueName, City, PostalCode,
		Status, CreatedAt, MinimumPrice, TicketSalesMode, ExternalTicketingUrl, ExternalWebsiteUrl,
		AcceptsVendorApplications, AcceptsPanelApplications
	)
	SELECT
		seedEvent.OwnerId,
		category.Id,
		seedEvent.Title,
		seedEvent.Description,
		seedEvent.StartsAt,
		seedEvent.EndsAt,
		seedEvent.VenueName,
		seedEvent.City,
		seedEvent.PostalCode,
		2,                                                            -- Published
		DATEADD(DAY, -60, @Now),
		CASE WHEN seedEvent.TicketSalesMode = 3 THEN 0 ELSE seedEvent.BasePrice END,
		seedEvent.TicketSalesMode,
		seedEvent.ExternalTicketingUrl,
		seedEvent.ExternalWebsiteUrl,
		CASE WHEN seedEvent.Number IN (1, 2, 4, 5, 8, 10, 11, 13, 14, 16, 17) THEN 1 ELSE 0 END,
		CASE WHEN seedEvent.Number IN (1, 2, 4, 5, 6, 8, 9, 10, 11, 12, 13, 14, 16, 20) THEN 1 ELSE 0 END
	FROM @SeedEvents AS seedEvent
	INNER JOIN dbo.EventCategories AS category ON category.Name = seedEvent.EventCategoryName;

	UPDATE seedEvent
	SET EventId = eventRecord.Id
	FROM @SeedEvents AS seedEvent
	INNER JOIN dbo.Events AS eventRecord ON eventRecord.Title = seedEvent.Title;

	INSERT INTO dbo.EventInterests (EventId, InterestCategoryId)
	SELECT DISTINCT seedEvent.EventId, interest.Id
	FROM @SeedEvents AS seedEvent
	CROSS APPLY (VALUES (seedEvent.InterestName), (seedEvent.SecondInterestName)) AS picked (Name)
	INNER JOIN dbo.InterestCategories AS interest ON interest.Name = picked.Name;

	/* --------------------------------------------------------------------
	   Ticket types (events that sell tickets here)
	   General Admission or Free RSVP on every one; VIP on paid events;
	   Student on some; an Early Bird that has stopped selling on a few
	   upcoming ones. Past events' sales ended when they started.
	   -------------------------------------------------------------------- */

	INSERT INTO dbo.EventTicketTypes (EventId, Name, Price, QuantityAvailable, QuantitySold, SalesStartAt, SalesEndAt, IsEnabled)
	SELECT seedEvent.EventId, tier.Name, tier.Price, tier.Capacity, 0, tier.SalesStartAt, tier.SalesEndAt, 1
	FROM @SeedEvents AS seedEvent
	CROSS APPLY (SELECT CASE WHEN seedEvent.IsPast = 1 THEN DATEADD(DAY, -45, seedEvent.StartsAt) ELSE DATEADD(DAY, -30, @Now) END AS SalesStartAt) AS sales
	CROSS APPLY (VALUES
		(CASE WHEN seedEvent.BasePrice = 0 THEN 'Free RSVP' ELSE 'General Admission' END,
			seedEvent.BasePrice, 200 + (seedEvent.Number % 5) * 50, sales.SalesStartAt, seedEvent.StartsAt, 1),
		('VIP', seedEvent.BasePrice * 2 + 20,
			CASE seedEvent.Number WHEN 2 THEN 6 WHEN 3 THEN 8 ELSE 25 END,
			sales.SalesStartAt, seedEvent.StartsAt,
			CASE WHEN seedEvent.BasePrice > 0 THEN 1 ELSE 0 END),
		('Student', CAST(seedEvent.BasePrice * 0.6 AS decimal(10, 2)), 60, sales.SalesStartAt, seedEvent.StartsAt,
			CASE WHEN seedEvent.BasePrice >= 25 AND seedEvent.Number % 2 = 0 THEN 1 ELSE 0 END),
		('Early Bird', CAST(seedEvent.BasePrice * 0.8 AS decimal(10, 2)), 50, sales.SalesStartAt, DATEADD(DAY, -2, @Now),
			CASE WHEN seedEvent.IsPast = 0 AND seedEvent.BasePrice > 0 AND seedEvent.Number % 5 = 1 THEN 1 ELSE 0 END)
	) AS tier (Name, Price, Capacity, SalesStartAt, SalesEndAt, Include)
	WHERE seedEvent.TicketSalesMode = 1
	  AND tier.Include = 1;

	-- "From $X" uses the cheapest tier that's still part of the event's pricing.
	UPDATE eventRecord
	SET MinimumPrice = cheapest.Price
	FROM dbo.Events AS eventRecord
	INNER JOIN @SeedEvents AS seedEvent ON seedEvent.EventId = eventRecord.Id
	CROSS APPLY
	(
		SELECT MIN(ticketType.Price) AS Price
		FROM dbo.EventTicketTypes AS ticketType
		WHERE ticketType.EventId = eventRecord.Id AND ticketType.Name <> 'Early Bird'
	) AS cheapest
	WHERE seedEvent.TicketSalesMode = 1;

	/* --------------------------------------------------------------------
	   4. Ticket orders
	   -------------------------------------------------------------------- */

	DECLARE @Purchases TABLE
	(
		PurchaseId int IDENTITY(1, 1) PRIMARY KEY,
		UserId nvarchar(450) NOT NULL,
		BuyerIdx int NOT NULL,
		EventNumber int NOT NULL,
		TicketTypeName nvarchar(100) NOT NULL,
		Quantity int NOT NULL,
		TicketTypeId int NULL,
		UnitPrice decimal(10, 2) NULL,
		OrderedAt datetimeoffset(7) NULL
	);

	-- Past events: about four attendees each, plus whichever test account
	-- doesn't organize it. These buyers write the reviews below.
	INSERT INTO @Purchases (UserId, BuyerIdx, EventNumber, TicketTypeName, Quantity)
	SELECT
		buyer.UserId,
		buyer.Idx,
		seedEvent.Number,
		CASE WHEN seedEvent.BasePrice = 0 THEN 'Free RSVP' WHEN buyer.Idx = 1 THEN 'VIP' ELSE 'General Admission' END,
		1 + (seedEvent.Number + buyer.Idx) % 2
	FROM @SeedEvents AS seedEvent
	CROSS JOIN @Buyers AS buyer
	WHERE seedEvent.IsPast = 1
	  AND seedEvent.TicketSalesMode = 1
	  AND buyer.UserId <> seedEvent.OwnerId
	  AND (buyer.Idx >= 7 OR (seedEvent.Number + buyer.Idx) % 3 <> 0);

	-- Upcoming events: each attendee holds tickets to a handful.
	INSERT INTO @Purchases (UserId, BuyerIdx, EventNumber, TicketTypeName, Quantity)
	SELECT
		buyer.UserId,
		buyer.Idx,
		seedEvent.Number,
		CASE WHEN seedEvent.BasePrice = 0 THEN 'Free RSVP' ELSE 'General Admission' END,
		1 + (seedEvent.Number + buyer.Idx) % 3
	FROM @SeedEvents AS seedEvent
	CROSS JOIN @Buyers AS buyer
	WHERE seedEvent.IsPast = 0
	  AND seedEvent.TicketSalesMode = 1
	  AND buyer.Idx <= 6
	  AND (seedEvent.Number + buyer.Idx * 5) % 7 = 0;

	-- Each test account holds tickets to two of the other's upcoming events,
	-- so My tickets and the announcement line have something to show.
	INSERT INTO @Purchases (UserId, BuyerIdx, EventNumber, TicketTypeName, Quantity)
	SELECT buyer.UserId, buyer.Idx, picked.EventNumber, picked.TicketTypeName, picked.Quantity
	FROM (VALUES
		(7, 6, 'Free RSVP', 2),          -- organizer@test.com at vendor events
		(7, 9, 'General Admission', 1),
		(8, 1, 'General Admission', 2),  -- vendor@test.com at organizer events
		(8, 4, 'General Admission', 1)
	) AS picked (BuyerIdx, EventNumber, TicketTypeName, Quantity)
	INNER JOIN @Buyers AS buyer ON buyer.Idx = picked.BuyerIdx;

	-- Scarcity: Event-Future-02's 6 VIP tickets sell out, and
	-- Event-Future-03 sells 5 of its 8 ("Only 3 left").
	INSERT INTO @Purchases (UserId, BuyerIdx, EventNumber, TicketTypeName, Quantity)
	SELECT buyer.UserId, buyer.Idx, picked.EventNumber, 'VIP', picked.Quantity
	FROM (VALUES (2, 2, 2), (3, 2, 2), (4, 2, 2), (5, 3, 2), (6, 3, 3)) AS picked (BuyerIdx, EventNumber, Quantity)
	INNER JOIN @Buyers AS buyer ON buyer.Idx = picked.BuyerIdx;

	UPDATE purchase
	SET
		TicketTypeId = ticketType.Id,
		UnitPrice = ticketType.Price,
		OrderedAt = CASE
			WHEN seedEvent.IsPast = 1
				THEN DATEADD(HOUR, -(48 + (purchase.EventNumber * 13 + purchase.BuyerIdx * 7) % 300), seedEvent.StartsAt)
			ELSE DATEADD(HOUR, -(24 + (purchase.EventNumber * 13 + purchase.BuyerIdx * 7) % 400), @Now)
		END
	FROM @Purchases AS purchase
	INNER JOIN @SeedEvents AS seedEvent ON seedEvent.Number = purchase.EventNumber
	INNER JOIN dbo.EventTicketTypes AS ticketType
		ON ticketType.EventId = seedEvent.EventId AND ticketType.Name = purchase.TicketTypeName;

	IF EXISTS (SELECT 1 FROM @Purchases WHERE TicketTypeId IS NULL)
	BEGIN
		THROW 50002, 'A seeded purchase refers to a ticket type that was not created.', 1;
	END;

	DECLARE @TicketOrders TABLE (PurchaseId int NOT NULL PRIMARY KEY, OrderId int NOT NULL);

	-- MERGE (rather than INSERT) so OUTPUT can pair each new order with its purchase row.
	MERGE dbo.Orders AS target
	USING @Purchases AS source
	ON 1 = 0
	WHEN NOT MATCHED THEN
		INSERT (UserId, Status, TotalAmount, CreatedAt, CompletedAt)
		VALUES (source.UserId, 1, source.UnitPrice * source.Quantity, source.OrderedAt, source.OrderedAt)
	OUTPUT source.PurchaseId, inserted.Id INTO @TicketOrders (PurchaseId, OrderId);

	INSERT INTO dbo.Tickets (OrderId, EventTicketTypeId, UnitPrice, Code)
	SELECT ticketOrder.OrderId, purchase.TicketTypeId, purchase.UnitPrice, NEWID()
	FROM @Purchases AS purchase
	INNER JOIN @TicketOrders AS ticketOrder ON ticketOrder.PurchaseId = purchase.PurchaseId
	INNER JOIN @Numbers AS n ON n.N <= purchase.Quantity;

	-- Keep each tier's sold count in step with the tickets that exist.
	UPDATE ticketType
	SET QuantitySold = sold.SoldCount
	FROM dbo.EventTicketTypes AS ticketType
	INNER JOIN
	(
		SELECT ticket.EventTicketTypeId, COUNT(*) AS SoldCount
		FROM dbo.Tickets AS ticket
		INNER JOIN dbo.Orders AS orderRecord ON orderRecord.Id = ticket.OrderId
		WHERE orderRecord.Status IN (0, 1)
		GROUP BY ticket.EventTicketTypeId
	) AS sold ON sold.EventTicketTypeId = ticketType.Id
	WHERE ticketType.EventId IN (SELECT EventId FROM @SeedEvents);

	/* --------------------------------------------------------------------
	   5. Reviews of past events, one per ticket holder
	   -------------------------------------------------------------------- */

	DECLARE @ReviewComments TABLE (Rating int NOT NULL, Variant int NOT NULL, Comment nvarchar(2000) NOT NULL, PRIMARY KEY (Rating, Variant));

	INSERT INTO @ReviewComments (Rating, Variant, Comment)
	VALUES
		(5, 0, 'Best event I''ve been to all year. The arcade area alone was worth the ticket.'),
		(5, 1, 'So well organized. Short lines, friendly staff, and the vendors had great stuff.'),
		(5, 2, 'Brought my kids and we all had a blast. Already looking forward to the next one!'),
		(4, 0, 'Really fun night. It got a little crowded around the main stage, but I''d go again.'),
		(4, 1, 'Great atmosphere and lots to do. Wish the food lines had moved a bit faster.'),
		(4, 2, 'Loved the music and the vendors. Parking was a bit of a hassle.'),
		(3, 0, 'Decent event, but it felt smaller than the description made it sound.'),
		(3, 1, 'Some highlights, but the schedule ran late and a couple of sessions were cut.'),
		(3, 2, 'Okay overall. The venue was too warm and the sound was hard to hear at the back.'),
		(2, 0, 'Disappointing. Half the advertised activities weren''t running when we got there.'),
		(2, 1, 'Long lines and not much to do once inside. Hoping they improve next time.'),
		(2, 2, 'The ticket price felt high for what was offered.');

	INSERT INTO dbo.EventReviews (EventId, AuthorUserId, Rating, Comment, CreatedAt, UpdatedAt)
	SELECT
		seedEvent.EventId,
		purchase.UserId,
		rating.Value,
		CASE WHEN (purchase.EventNumber + purchase.BuyerIdx) % 5 = 0 THEN NULL ELSE comment.Comment END,
		CASE WHEN reviewTime.At > @Now THEN @Now ELSE reviewTime.At END,
		NULL
	FROM @Purchases AS purchase
	INNER JOIN @SeedEvents AS seedEvent ON seedEvent.Number = purchase.EventNumber AND seedEvent.IsPast = 1
	-- Mostly 4s and 5s with the odd 3 and 2, for realistic averages.
	CROSS APPLY (SELECT CASE (purchase.EventNumber * 7 + purchase.BuyerIdx * 3) % 10
		WHEN 0 THEN 5 WHEN 1 THEN 4 WHEN 2 THEN 5 WHEN 3 THEN 3 WHEN 4 THEN 5
		WHEN 5 THEN 4 WHEN 6 THEN 2 WHEN 7 THEN 5 WHEN 8 THEN 4 ELSE 5 END AS Value) AS rating
	CROSS APPLY (SELECT DATEADD(HOUR, 3 + (purchase.EventNumber * 5 + purchase.BuyerIdx * 11) % 40, seedEvent.EndsAt) AS At) AS reviewTime
	INNER JOIN @ReviewComments AS comment
		ON comment.Rating = rating.Value AND comment.Variant = (purchase.EventNumber + purchase.BuyerIdx) % 3;

	/* --------------------------------------------------------------------
	   6a. Announcements
	   -------------------------------------------------------------------- */

	INSERT INTO dbo.EventAnnouncements (EventId, Title, Body, IsPinned, PostedByUserId, PostedByName, CreatedAt, UpdatedAt)
	SELECT
		seedEvent.EventId,
		post.Title,
		post.Body,
		post.IsPinned,
		seedEvent.OwnerId,
		seedEvent.OwnerName,
		CASE WHEN seedEvent.IsPast = 1 THEN DATEADD(HOUR, 20, seedEvent.EndsAt) ELSE DATEADD(HOUR, -post.HoursAgo, @Now) END,
		NULL
	FROM (VALUES
		(1, 'Doors open 30 minutes early', 'We''re opening the doors early to help with lines. Have your ticket ready on your phone or printed.', 1, 6),
		(1, 'Free parking in Lot B', 'Lot B behind the venue is free for ticket holders all day. Follow the signs from the main entrance.', 0, 72),
		(2, 'VIP is sold out', 'Thank you! VIP tickets are gone, but General Admission is still available.', 0, 20),
		(3, 'Room change: now in Hall C', 'We''ve moved to the larger Hall C to fit everyone. Same time, just down the corridor.', 1, 30),
		(4, 'Speaker lineup announced', 'The full lineup is up on our website. Expect talks on game preservation, collecting, and console repair.', 0, 120),
		(5, 'Moving indoors', 'Rain is in the forecast, so the whole event is moving inside. Nothing else changes.', 1, 10),
		(6, 'RSVP reminder', 'It''s free, but please RSVP so we know how many people to expect.', 0, 200),
		(8, 'Vendor hall map posted', 'Find your favorite vendors faster: the hall map is posted at the entrance and on our site.', 0, 50),
		(10, 'Schedule released', 'Panels start at noon and run every hour. The tournament finals start at 5 PM.', 0, 90),
		(12, 'Garage closed: use street parking', 'The venue garage is closed for repairs that day. Street parking is free after 6 PM.', 1, 4),
		(16, 'Early bird pricing has ended', 'Thanks to everyone who grabbed early bird tickets! Regular pricing is now in effect.', 0, 300),
		(20, 'Volunteers wanted', 'Want to help out and get in free? Message us through our website to sign up for a shift.', 0, 400),
		(38, 'Thanks for coming!', 'What a night. Photos are going up on our website this week. Leave us a review and tell us what to bring back.', 0, 0)
	) AS post (EventNumber, Title, Body, IsPinned, HoursAgo)
	INNER JOIN @SeedEvents AS seedEvent ON seedEvent.Number = post.EventNumber;

	/* --------------------------------------------------------------------
	   6b. Panels: approved ones are listed on the event page; pending ones
	   fill the organizer's review queue.
	   -------------------------------------------------------------------- */

	INSERT INTO dbo.PanelApplications
	(
		EventId, ApplicantUserId, Status, Title, Description, Panelists, DurationMinutes, ContactEmail,
		Message, SubmittedAt, DecidedAt, ReviewedByUserId, ReviewedByName, ReviewNote
	)
	SELECT
		seedEvent.EventId,
		buyer.UserId,
		panel.Status,
		panel.Title,
		panel.Description,
		panel.Panelists,
		panel.DurationMinutes,
		buyer.Email,
		NULL,
		DATEADD(DAY, -(panel.DaysAgo + 5), @Now),
		CASE WHEN panel.Status = 1 THEN DATEADD(DAY, -panel.DaysAgo, @Now) END,
		CASE WHEN panel.Status = 1 THEN seedEvent.OwnerId END,
		CASE WHEN panel.Status = 1 THEN seedEvent.OwnerName END,
		NULL
	FROM (VALUES
		-- Approved
		(1, 1, 1, 'Speedrunning the Classics', 'Top runners break down the tricks behind world-record runs of classic games, with live demos.', 'Alex R. - hobby speedrunner
Priya S. - tournament organizer', 60, 4),
		(2, 2, 1, 'Collecting on a Budget', 'How to build a retro collection without overspending: where to look, what to skip, and spotting fakes.', 'Jordan L. - collector and reseller
Dana M. - thrift store owner', 45, 6),
		(4, 3, 1, 'A Short History of Home Consoles', 'From wood-grain to wireless: a tour of the machines that changed how we play at home.', 'Sam P. - museum volunteer
Lee K. - games historian', 60, 3),
		(5, 4, 1, 'Chiptune 101', 'Make music with old sound chips. Bring headphones; we''ll provide the gear.', 'Taylor B. - chiptune musician', 90, 8),
		(6, 5, 1, 'Restoring CRT Televisions', 'Safety first: how to clean, repair and calibrate old CRTs for the best retro picture.', 'Morgan C. - electronics hobbyist
Chris D. - repair shop owner', 60, 2),
		(9, 6, 1, 'Indie Devs Making Retro Games', 'Local developers on why they build new games for old hardware, and how you can start.', 'Casey N. - indie developer
Sam P. - pixel artist
Robin T. - composer', 60, 5),
		(10, 1, 1, 'Saturday Morning Cartoons Trivia', 'A team trivia showdown all about the cartoons, cereals and toys of Saturday mornings.', 'Alex R. - quizmaster', 45, 7),
		(12, 2, 1, 'Preserving Game History', 'Why old games disappear and what archivists, libraries and fans are doing to save them.', 'Jordan L. - archivist
Morgan C. - librarian', 60, 1),
		-- Pending (waiting for the organizer)
		(8, 3, 0, 'Pixel Art Workshop', 'A beginner-friendly session on drawing sprites and tiles with free tools.', 'Sam P. - pixel artist', 90, 0),
		(10, 4, 0, 'Arcade Cabinet Repair Q&A', 'Bring your questions about fixing up arcade cabinets, from monitors to coin doors.', 'Taylor B. - cabinet restorer', 45, 0),
		(11, 5, 0, 'Retro Fashion Through the Decades', 'A fun look at the clothes of the 70s, 80s and 90s, with a costume contest at the end.', 'Morgan C. - costume designer', 60, 0),
		(13, 6, 0, 'Board Games of the 80s', 'A tabletop panel and open-play session with classic board games from the 80s.', 'Casey N. - board game cafe owner', 120, 0),
		(6, 2, 0, 'Modding Your Handheld', 'Backlights, new shells and better batteries: how to upgrade old handheld consoles.', 'Jordan L. - modder', 60, 0)
	) AS panel (EventNumber, BuyerIdx, Status, Title, Description, Panelists, DurationMinutes, DaysAgo)
	INNER JOIN @SeedEvents AS seedEvent ON seedEvent.Number = panel.EventNumber
	INNER JOIN @Buyers AS buyer ON buyer.Idx = panel.BuyerIdx;

	/* --------------------------------------------------------------------
	   7. vendor@test.com's vendor profile, online approval and products
	   -------------------------------------------------------------------- */

	DECLARE @VendorProfileId int = (SELECT Id FROM dbo.VendorProfiles WHERE OwnerUserId = @VendorId);

	IF @VendorProfileId IS NULL
	BEGIN
		INSERT INTO dbo.VendorProfiles (OwnerUserId, BusinessName, ProductsOffered, Description, ContactEmail, WebsiteUrl, CreatedAt, UpdatedAt)
		VALUES
		(
			@VendorId,
			'Rewind Collectibles',
			'Retro apparel, pins, posters and gaming accessories',
			'A small family shop selling merchandise inspired by the games, music and movies of the 80s and 90s. Find us at conventions around the Midwest.',
			'vendor@test.com',
			'https://example.com/rewind-collectibles',
			DATEADD(DAY, -90, @Now),
			DATEADD(DAY, -90, @Now)
		);

		SET @VendorProfileId = SCOPE_IDENTITY();
	END;

	-- Products only appear in the shop once the vendor is approved to sell online.
	IF NOT EXISTS
	(
		SELECT 1 FROM dbo.VendorApplications
		WHERE VendorProfileId = @VendorProfileId AND Kind = 0 AND Status = 1
	)
	BEGIN
		INSERT INTO dbo.VendorApplications
		(
			VendorProfileId, Kind, EventId, Status, BusinessName, ProductsOffered, Description, ContactEmail, WebsiteUrl,
			Message, SubmittedAt, DecidedAt, ReviewedByUserId, ReviewedByName, ReviewNote
		)
		SELECT
			Id, 0, NULL, 1, BusinessName, ProductsOffered, Description, ContactEmail, WebsiteUrl,
			NULL, DATEADD(DAY, -80, @Now), DATEADD(DAY, -78, @Now), NULL, 'Site admin (demo seed)', NULL
		FROM dbo.VendorProfiles
		WHERE Id = @VendorProfileId;
	END;

	-- Approved to sell at some of organizer@test.com's upcoming events, so
	-- those pages list the vendor and the storefront shows "Find them in person".
	INSERT INTO dbo.VendorApplications
	(
		VendorProfileId, Kind, EventId, Status, BusinessName, ProductsOffered, Description, ContactEmail, WebsiteUrl,
		Message, SubmittedAt, DecidedAt, ReviewedByUserId, ReviewedByName, ReviewNote
	)
	SELECT
		profile.Id, 1, seedEvent.EventId, 1, profile.BusinessName, profile.ProductsOffered, profile.Description,
		profile.ContactEmail, profile.WebsiteUrl, 'Looking forward to it! We''ll bring a full table.',
		DATEADD(DAY, -20, @Now), DATEADD(DAY, -18, @Now), @OrganizerId, @OrganizerName, NULL
	FROM dbo.VendorProfiles AS profile
	CROSS JOIN @SeedEvents AS seedEvent
	WHERE profile.Id = @VendorProfileId
	  AND seedEvent.Number IN (1, 2, 4, 5, 10);

	INSERT INTO dbo.VendorProducts
	(
		VendorProfileId, Name, Description, Price, QuantityInStock, IsListed, ImageId, CreatedAt, UpdatedAt, ArchivedAt
	)
	SELECT
		@VendorProfileId, Name, Description, Price, QuantityInStock, IsListed, NULL,
		DATEADD(HOUR, -(SortOrder * 30), @Now), DATEADD(HOUR, -(SortOrder * 30), @Now), NULL
	FROM @SeedProducts;

	/* Merchandise orders: five attendees and organizer@test.com, one order each. */
	DECLARE @MerchLines TABLE (BuyerIdx int NOT NULL, ProductName nvarchar(150) NOT NULL, Quantity int NOT NULL);

	INSERT INTO @MerchLines (BuyerIdx, ProductName, Quantity)
	VALUES
		(1, 'Retro Console Tee - Black', 1), (1, 'Pixel Heart Enamel Pin', 2),
		(2, 'Synthwave Sunset Poster 18x24', 1), (2, 'VHS Rewind Mug', 1),
		(3, 'Boombox Bluetooth Speaker', 1),
		(4, '80s Neon Socks (3 pairs)', 2), (4, '8-Bit Controller Keychain', 1),
		(6, 'Cassette Tape Coaster Set (4)', 1), (6, 'Mixtape Sticker Pack (20)', 3),
		(7, 'VHS Rewind Mug', 1), (7, 'Pixel Heart Enamel Pin', 1);

	DECLARE @MerchOrders TABLE (BuyerIdx int NOT NULL PRIMARY KEY, OrderId int NOT NULL);

	MERGE dbo.Orders AS target
	USING
	(
		SELECT
			line.BuyerIdx,
			buyer.UserId,
			SUM(product.Price * line.Quantity) AS Total,
			DATEADD(HOUR, -(30 + line.BuyerIdx * 17), @Now) AS OrderedAt
		FROM @MerchLines AS line
		INNER JOIN @Buyers AS buyer ON buyer.Idx = line.BuyerIdx
		INNER JOIN @SeedProducts AS product ON product.Name = line.ProductName
		GROUP BY line.BuyerIdx, buyer.UserId
	) AS source
	ON 1 = 0
	WHEN NOT MATCHED THEN
		INSERT (UserId, Status, TotalAmount, CreatedAt, CompletedAt)
		VALUES (source.UserId, 1, source.Total, source.OrderedAt, source.OrderedAt)
	OUTPUT source.BuyerIdx, inserted.Id INTO @MerchOrders (BuyerIdx, OrderId);

	INSERT INTO dbo.OrderItems (OrderId, VendorProductId, ProductName, VendorName, UnitPrice, Quantity)
	SELECT merchOrder.OrderId, product.Id, product.Name, profile.BusinessName, product.Price, line.Quantity
	FROM @MerchLines AS line
	INNER JOIN @MerchOrders AS merchOrder ON merchOrder.BuyerIdx = line.BuyerIdx
	INNER JOIN dbo.VendorProducts AS product ON product.VendorProfileId = @VendorProfileId AND product.Name = line.ProductName
	INNER JOIN dbo.VendorProfiles AS profile ON profile.Id = product.VendorProfileId;

	/* --------------------------------------------------------------------
	   6c. Pending applications for the review queues
	   -------------------------------------------------------------------- */

	-- morgan.demo@test.com runs a second vendor, "Pixel Pin Co.":
	-- pending online sales (admin queue), pending at events 8 and 11
	-- (organizer queue), and approved at event 13 without an online shop.
	DECLARE @PinVendorProfileId int;

	INSERT INTO dbo.VendorProfiles (OwnerUserId, BusinessName, ProductsOffered, Description, ContactEmail, WebsiteUrl, CreatedAt, UpdatedAt)
	SELECT
		Id,
		'Pixel Pin Co.',
		'Handmade enamel pins and patches',
		'One-person studio making small-batch enamel pins and embroidered patches with a pixel-art twist.',
		Email,
		NULL,
		DATEADD(DAY, -15, @Now),
		DATEADD(DAY, -15, @Now)
	FROM @DemoUsers
	WHERE Idx = 5;

	SET @PinVendorProfileId = SCOPE_IDENTITY();

	INSERT INTO dbo.VendorApplications
	(
		VendorProfileId, Kind, EventId, Status, BusinessName, ProductsOffered, Description, ContactEmail, WebsiteUrl,
		Message, SubmittedAt, DecidedAt, ReviewedByUserId, ReviewedByName, ReviewNote
	)
	SELECT
		profile.Id, request.Kind, seedEvent.EventId, request.Status, profile.BusinessName, profile.ProductsOffered,
		profile.Description, profile.ContactEmail, profile.WebsiteUrl, request.Message,
		DATEADD(DAY, -request.DaysAgo, @Now),
		CASE WHEN request.Status = 1 THEN DATEADD(DAY, -(request.DaysAgo - 1), @Now) END,
		CASE WHEN request.Status = 1 THEN @OrganizerId END,
		CASE WHEN request.Status = 1 THEN @OrganizerName END,
		NULL
	FROM (VALUES
		(0, NULL, 0, 'I''d love to sell my pins online as well as at events.', 2),
		(1, 8, 0, 'We need about half a table and no power.', 3),
		(1, 11, 0, NULL, 1),
		(1, 13, 1, 'First convention for us. Excited to be there!', 9)
	) AS request (Kind, EventNumber, Status, Message, DaysAgo)
	CROSS JOIN dbo.VendorProfiles AS profile
	LEFT JOIN @SeedEvents AS seedEvent ON seedEvent.Number = request.EventNumber
	WHERE profile.Id = @PinVendorProfileId;

	-- Two people asking to host events (admin queue).
	INSERT INTO dbo.EventApplications
	(
		ApplicantUserId, Status, Title, Description, EventCategoryId, StartsAt, EndsAt, VenueName, City, PostalCode,
		ExternalWebsiteUrl, BackgroundImageId, ContactName, ContactEmail, ContactPhone, OrganizationName, Message,
		SubmittedAt, DecidedAt, ReviewedByUserId, ReviewedByName, ReviewNote, CreatedEventId
	)
	SELECT
		demoUser.Id, 0, request.Title, request.Description, category.Id,
		TODATETIMEOFFSET(DATEADD(HOUR, request.StartHour, CAST(DATEADD(DAY, request.DaysOut, @Today) AS datetime2(7))), '-06:00'),
		TODATETIMEOFFSET(DATEADD(HOUR, request.StartHour + request.Hours, CAST(DATEADD(DAY, request.DaysOut, @Today) AS datetime2(7))), '-06:00'),
		request.Venue, request.City, request.PostalCode, NULL, NULL,
		demoUser.DisplayName, demoUser.Email, '555-0100', request.Organization, request.Message,
		DATEADD(HOUR, -request.HoursAgo, @Now), NULL, NULL, NULL, NULL, NULL
	FROM (VALUES
		(3, 'Event-Application-Pending-01', 'Gaming',
			'A weekend LAN party with classic PC games, a modding showcase and a swap meet for old hardware.',
			45, 10, 8, 'Capitol Square Pavilion', 'Madison', '53703', 'Madison Retro PC Club', 'We ran this as a club meetup last year with about 80 people.', 20),
		(6, 'Event-Application-Pending-02', 'Concert',
			'An evening of 80s synth-pop covers from three local bands, with a costume contest between sets.',
			60, 19, 4, 'Lakeside Expo Center', 'Chicago', '60601', NULL, NULL, 50)
	) AS request (Idx, Title, CategoryName, Description, DaysOut, StartHour, Hours, Venue, City, PostalCode, Organization, Message, HoursAgo)
	INNER JOIN @DemoUsers AS demoUser ON demoUser.Idx = request.Idx
	INNER JOIN dbo.EventCategories AS category ON category.Name = request.CategoryName;

	INSERT INTO dbo.EventApplicationInterests (EventApplicationId, InterestCategoryId)
	SELECT application.Id, interest.Id
	FROM dbo.EventApplications AS application
	INNER JOIN (VALUES
		('Event-Application-Pending-01', 'Gaming'),
		('Event-Application-Pending-01', 'Technology'),
		('Event-Application-Pending-02', 'Music')
	) AS picked (Title, InterestName) ON picked.Title = application.Title
	INNER JOIN dbo.InterestCategories AS interest ON interest.Name = picked.InterestName
	WHERE application.ApplicantUserId IN (SELECT Id FROM @DemoUsers);

	/* --------------------------------------------------------------------
	   Saved events (popular events sort first on the Events page)
	   -------------------------------------------------------------------- */

	INSERT INTO dbo.SavedEvents (UserId, EventId, SavedAt)
	SELECT demoUser.Id, seedEvent.EventId, DATEADD(HOUR, -(seedEvent.Number * 5 + demoUser.Idx), @Now)
	FROM @DemoUsers AS demoUser
	CROSS JOIN @SeedEvents AS seedEvent
	WHERE seedEvent.IsPast = 0
	  AND ((seedEvent.Number + demoUser.Idx) % 6 = 0 OR seedEvent.Number IN (1, 5));

	COMMIT TRANSACTION;
END TRY
BEGIN CATCH
	IF @@TRANCOUNT > 0
	BEGIN
		ROLLBACK TRANSACTION;
	END;

	THROW;
END CATCH;

/* ------------------------------------------------------------------------
   Summary
   ------------------------------------------------------------------------ */

SELECT
	organizer.Email AS Organizer,
	SUM(CASE WHEN eventRecord.EndsAt > SYSDATETIMEOFFSET() THEN 1 ELSE 0 END) AS UpcomingEvents,
	SUM(CASE WHEN eventRecord.EndsAt <= SYSDATETIMEOFFSET() THEN 1 ELSE 0 END) AS PastEvents
FROM dbo.Events AS eventRecord
INNER JOIN dbo.AspNetUsers AS organizer ON organizer.Id = eventRecord.OrganizerId
WHERE eventRecord.Title LIKE 'Event-Future-%' OR eventRecord.Title LIKE 'Event-Date-Passed-%'
GROUP BY organizer.Email;

SELECT
	(SELECT COUNT(*) FROM dbo.EventTicketTypes AS t INNER JOIN dbo.Events AS e ON e.Id = t.EventId
	 WHERE e.Title LIKE 'Event-%') AS TicketTypes,
	(SELECT COUNT(*) FROM dbo.Tickets AS t INNER JOIN dbo.EventTicketTypes AS tt ON tt.Id = t.EventTicketTypeId
	 INNER JOIN dbo.Events AS e ON e.Id = tt.EventId WHERE e.Title LIKE 'Event-%') AS TicketsSold,
	(SELECT COUNT(*) FROM dbo.EventReviews AS r INNER JOIN dbo.Events AS e ON e.Id = r.EventId
	 WHERE e.Title LIKE 'Event-Date-Passed-%') AS Reviews,
	(SELECT COUNT(*) FROM dbo.VendorProducts AS p INNER JOIN dbo.VendorProfiles AS v ON v.Id = p.VendorProfileId
	 INNER JOIN dbo.AspNetUsers AS u ON u.Id = v.OwnerUserId WHERE u.NormalizedEmail = 'VENDOR@TEST.COM' AND p.ArchivedAt IS NULL) AS VendorProducts,
	(SELECT COUNT(*) FROM dbo.AspNetUsers WHERE NormalizedEmail LIKE '%.DEMO@TEST.COM') AS DemoAttendees;
